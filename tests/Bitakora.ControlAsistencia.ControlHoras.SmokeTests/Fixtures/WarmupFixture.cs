using System.Diagnostics;
using System.Net.Http.Json;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

// Issue #166: warm-up funcional de la cadena Service Bus.
//
// Problema (H1, cold start del listener SB): tras ventanas largas de inactividad el host de
// Functions se duerme (B1 sin always_on, descartado por costo - ver ADR-0009). Un health-ping
// HTTP solo confirma "host HTTP arriba", no "listeners SB consumiendo": la inicializacion del
// listener (link AMQP + lease de la subscription) es asincrona y posterior al 200. El test
// DebePublicarDiaDepuradoYPersistirMarcacionAdicionada... ya ceba implicitamente AsignarTurno
// en su setup, pero AdicionarMarcacion arranca frio justo en el Act y los 30s no alcanzan.
//
// Solucion: ejecutar la cadena SB completa UNA sola vez, con identificadores descartables,
// antes de toda la suite. La unica senal fiable de "listener vivo" es que procese un mensaje,
// asi que el cebado publica y espera la persistencia de cada salto:
//   1. programacion-turno-diario-solicitada -> listener AsignarTurno -> turno_diario_asignado
//   2. POST marcacion (registro-de-marcacion-creado) -> listener AdicionarMarcacion -> marcacion_adicionada
//
// Nota xUnit v3: un AssemblyFixture NO puede inyectar otro AssemblyFixture
// (https://github.com/xunit/xunit/issues/3469 - "unresolved constructor arguments"; los fixtures
// exigen ctor sin parametros y no controlan su orden de creacion). El patron recomendado por el
// equipo de xUnit es que la clase contenedora construya ella misma sus dependencias. Por eso este
// fixture instancia y reutiliza las clases de fixture (cada una lee su propia config) en lugar de
// duplicar la lectura de configuracion o las primitivas de SB/Postgres/HTTP.
public class WarmupFixture : IAsyncLifetime
{
    private const string Ruta = "/api/control-horas/marcaciones";
    private const string SchemaControlHoras = "control_horas";
    private const string TopicProgramacionEntrada = "programacion-turno-diario-solicitada";
    private const string TipoEventoTurnoDiarioAsignado = "turno_diario_asignado";
    private const string TipoEventoMarcacionAdicionada = "marcacion_adicionada";

    // CA-2: timeout amplio (>= 90s) para absorber el cold start del listener SB tras inactividad.
    // Este es el unico punto donde se paga el arranque frio, controlado y una sola vez. NO se usa
    // para inflar el Timeout = 30s de las aserciones de los tests reales.
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(120);

    public async ValueTask InitializeAsync()
    {
        // Reutilizamos las clases de fixture (ver nota de cabecera): construimos instancias propias
        // y las inicializamos nosotros mismos. No comparten estado con las que xUnit inyecta a los
        // tests; son conexiones aparte que solo viven durante el cebado.
        var api = new ApiFixture();
        var serviceBus = new ServiceBusFixture();
        var postgres = new PostgresFixture();

        try
        {
            await api.InitializeAsync();
            await serviceBus.InitializeAsync();
            await postgres.InitializeAsync();

            if (!serviceBus.IsConfigured || !postgres.IsConfigured)
            {
                Log("ServiceBus o Postgres no configurado; se omite el cebado " +
                    "(mismo criterio de skip que los smoke tests de la cadena SB).");
                return;
            }

            await CebarCadenaAsync(api, serviceBus, postgres);
        }
        catch (Exception ex)
        {
            // CA-3: el fallo del cebado es visible y diagnosticable, pero NO se relanza. El warm-up
            // es best-effort a proposito: un gate que tirara reintroduciria flakiness (podria matar
            // la suite justo cuando los listeners terminaron de calentarse tras el timeout). Los
            // tests reales corren igual con su Timeout = 30s honesto; si el handler tiene una
            // regresion real, ESE test falla y la regresion se ve. El cebado no la enmascara.
            Log($"FALLO durante el cebado: {ex.GetType().Name}: {ex.Message}. " +
                "Los tests de la cadena SB correran con su Timeout = 30s sin modificar.");
        }
        finally
        {
            await postgres.DisposeAsync();
            await serviceBus.DisposeAsync();
            await api.DisposeAsync();
        }
    }

    private static async Task CebarCadenaAsync(
        ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
    {
        var stopwatch = Stopwatch.StartNew();

        // CA-4: identificadores descartables y unicos. El stream cd:{codigoColaborador}:{fecha} queda
        // aislado; no toca los streams ni las suscripciones que verifican los tests reales. No leemos
        // ni purgamos la suscripcion smoke-tests de dia-depurado: el
        // DiaDepurado que emite este cebado lleva un CodigoColaborador distinto (los tests filtran por
        // el suyo) y, ademas, el test real purga esa suscripcion antes de su Act. Confirmamos el cebado
        // solo via Postgres, que ya prueba que ambos listeners procesaron.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 1, 1);
        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

        // Salto 1: publicar programacion-turno-diario-solicitada -> calienta el listener AsignarTurno,
        // que persiste turno_diario_asignado en el stream.
        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = new
            {
                CodigoColaborador = codigoColaborador,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "000000000",
                Nombres = "[WARMUP] Cebado cadena SB",
                Apellidos = "[WARMUP] Issue 166"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = "[WARMUP] Turno cebado",
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>()
                    }
                }
            }
        };

        await serviceBus.PublishAsync(TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, WarmupTimeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        if (!turnoAsignado)
            throw new TimeoutException(
                $"El listener AsignarTurno no persistio {TipoEventoTurnoDiarioAsignado} " +
                $"en el stream {streamId} dentro de {WarmupTimeout.TotalSeconds}s.");

        // Salto 2: POST de la marcacion (emite registro-de-marcacion-creado) -> calienta el listener
        // AdicionarMarcacion, que persiste marcacion_adicionada en el mismo stream. Marcacion
        // dentro de la franja programada (entrada 08:00-16:00), fuera de ventana nocturna.
        var timestamp = new DateTime(fecha, new TimeOnly(8, 0, 0), DateTimeKind.Utc);
        var marcacionPayload = new
        {
            codigoColaborador,
            timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
            tipoMarcacion = "ENTRADA",
            dispositivoId = "[WARMUP] DEV-SMOKE-166"
        };

        var response = await api.Client.PostAsJsonAsync(Ruta, marcacionPayload);
        response.EnsureSuccessStatusCode();

        var marcacionAdicionada = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoMarcacionAdicionada, WarmupTimeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        if (!marcacionAdicionada)
            throw new TimeoutException(
                $"El listener AdicionarMarcacion no persistio {TipoEventoMarcacionAdicionada} " +
                $"en el stream {streamId} dentro de {WarmupTimeout.TotalSeconds}s.");

        stopwatch.Stop();
        Log($"OK: cadena SB cebada (AsignarTurno + AdicionarMarcacion) en " +
            $"{stopwatch.Elapsed.TotalSeconds:F1}s. Los listeners quedan calientes para la suite.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // CA-3: visible y "no se traga en silencio". Console.Error aparece en el log del job de CI con
    // verbosidad por defecto; SendDiagnosticMessage es el canal idiomatico de xUnit v3 (IMessageSink
    // dejo de inyectarse en fixtures en v3 - https://github.com/xunit/xunit/issues/3001). El mensaje
    // no contiene llaves para ser inocuo ante el overload de formato compuesto.
    private static void Log(string mensaje)
    {
        var linea = $"[WARM-UP issue#166] {mensaje}";
        Console.Error.WriteLine(linea);
        TestContext.Current?.SendDiagnosticMessage(linea);
    }
}
