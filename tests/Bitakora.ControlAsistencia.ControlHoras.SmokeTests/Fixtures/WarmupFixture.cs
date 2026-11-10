using System.Diagnostics;
using System.Net.Http.Json;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

// Warm-up funcional de la cadena Service Bus. Tras inactividad el host se duerme (B1 sin always_on,
// ADR-0009) y un health-ping solo confirma el host HTTP: el listener SB (link AMQP + lease) arranca
// despues del 200 y los 30 s de las aserciones no alcanzan. La unica senal fiable de listener vivo es
// que procese un mensaje, asi que la cadena completa se ejecuta una vez antes de la suite.
// xUnit v3 no inyecta un AssemblyFixture en otro (https://github.com/xunit/xunit/issues/3469): este
// fixture construye sus propias instancias de los demas.
public class WarmupFixture : IAsyncLifetime
{
    private const string Ruta = "/api/control-horas/marcaciones";
    private const string SchemaControlHoras = "control_horas";
    private const string TopicProgramacionEntrada = ProgramacionEnLote.Topic;
    private const string TipoEventoTurnoDiarioAsignado = "turno_diario_asignado";
    private const string TipoEventoMarcacionAdicionada = "marcacion_adicionada";

    // Solo el cebado paga el arranque frio; no usarlo para inflar el Timeout de 30 s de los tests.
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(120);

    public async ValueTask InitializeAsync()
    {
        // Instancias propias: no comparten estado con las que xUnit inyecta a los tests.
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
            // Best-effort a proposito: relanzar reintroduciria flakiness. Si hay una regresion real, la
            // detecta el test con su Timeout de 30 s; el cebado no la enmascara.
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

        // Identificadores descartables: el DiaDepurado del cebado no interfiere (los tests filtran por su
        // CodigoColaborador y purgan antes del Act), asi que el cebado se confirma solo via Postgres.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 1, 1);
        var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";

        var solicitudId = Guid.CreateVersion7();
        var programacionPayload = new
        {
            SolicitudId = solicitudId,
            Colaborador = new ResumenColaborador(
                "CC-000000000", codigoColaborador, "[WARMUP] Cebado cadena SB"),
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
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

        await serviceBus.PublishAsync(codigoColaborador, TopicProgramacionEntrada, programacionPayload, solicitudId.ToString());

        var turnoAsignado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoTurnoDiarioAsignado, WarmupTimeout,
            campoJson: "SolicitudId", valorJson: solicitudId.ToString());

        if (!turnoAsignado)
            throw new TimeoutException(
                $"El listener AsignarTurno no persistio {TipoEventoTurnoDiarioAsignado} " +
                $"en el stream {streamId} dentro de {WarmupTimeout.TotalSeconds}s.");

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

    // Console.Error sale en el log de CI; SendDiagnosticMessage es el canal de xUnit v3 (IMessageSink
    // ya no se inyecta en fixtures: https://github.com/xunit/xunit/issues/3001). El mensaje no lleva
    // llaves por el overload de formato compuesto.
    private static void Log(string mensaje)
    {
        var linea = $"[WARM-UP issue#166] {mensaje}";
        Console.Error.WriteLine(linea);
        TestContext.Current?.SendDiagnosticMessage(linea);
    }
}
