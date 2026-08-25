// Smoke tests de ObtenerDepuracionDelDia -- GET control-horas/depuraciones/{codigoColaborador}/{fecha}.
// Superficie de INVESTIGACION del Aprobador (issue #429): via (b1), SIN proyeccion materializada --
// la vista la produce DiaCalculadoAggregateRoot.GenerarDepuracionDelDia() al vuelo, sobre el
// aggregate hidratado en vivo (session.Events.AggregateStreamAsync, MEF-ADR-0015/0035). El arrange
// usa el mismo mecanismo que RecibirDepuracionViaSbSmokeTests y ListarAsistenciasDiariasSmokeTests:
// se publica DiaDepurado al bus interno; el consumidor de ControlHoras persiste depuracion_dia_recibida
// en el stream dc:{codigo}:{yyyyMMdd}. Esa persistencia es asincrona (el consumidor de Service Bus
// procesa el mensaje despues de publicarlo), asi que un GET inmediato puede devolver 404
// legitimamente -- el caso de exito envuelve la consulta en Polling.WaitUntilAsync (timeout estandar
// 30s); agotar el timeout es un fallo real (consumidor no desplegado o suscripcion sin registrar),
// nunca un skip.
//
// Quedan ROJOS hasta que el deploy publique la Function en dev: mientras tanto la ruta no existe y
// el host responde 404 a todo. El CI de PR no los ejecuta (solo corre *.Tests); su veredicto real se
// lee despues del deploy.
//
// Formas locales DESACOPLADAS de ReadModels y del Function App (isla, MEF-ADR-0039 decision 6): los
// enums locales replican el ORDEN de valores de produccion porque STJ los serializa como el entero
// subyacente -- si produccion reordenara alguno, la comparacion falla y delata el cambio de
// contrato.
//
// No se repiten aqui las derivaciones de negocio que ya cubre el unit test del generador
// (projection-test-writer, CA-1..CA-4 del issue): Plan=Descanso, Plan=SinProgramar y el dia nacido
// solo por marcacion (colaborador null). Este smoke test es black-box: un solo camino feliz con
// mezcla de Usada true/false alcanza para verificar que el endpoint desplegado devuelve la vista
// real que produce el aggregate, no solo el status code.
//
// CA-7 (tenant scoping de la QuerySession) no tiene superficie observable via HTTP negro-caja en un
// entorno de un solo tenant (CA-ADR-0027): lo cubre el test de composicion de la Function.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ObtenerDepuracionDelDia;

public class ObtenerDepuracionDelDiaSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicDiaDepurado = "dia-depurado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase (ComposicionServicios fija JsonNamingPolicy.CamelCase) y las
    // formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private enum EstadoAsistenciaSmoke
    {
        Provisional,
        Aprobado
    }

    private enum PlanDelDiaSmoke
    {
        ConJornada,
        Descanso,
        SinProgramar
    }

    private sealed record FranjaDepuradaSmoke(
        TimeOnly HoraInicioProgramada,
        TimeOnly HoraFinProgramada,
        int DiaOffsetFin,
        DateTime? Entrada,
        DateTime? Salida,
        bool EsAnomala);

    private sealed record MarcacionDelDiaSmoke(DateTime Timestamp, string? Tipo, bool Usada);

    private sealed record DepuracionDelDiaSmoke(
        string CodigoColaborador,
        DateOnly Fecha,
        string? IdentificacionColaborador,
        string? NombreColaborador,
        EstadoAsistenciaSmoke Estado,
        PlanDelDiaSmoke Plan,
        string? NombreTurno,
        IReadOnlyList<FranjaDepuradaSmoke> Franjas,
        IReadOnlyList<MarcacionDelDiaSmoke> Marcaciones,
        IReadOnlyDictionary<string, decimal> HorasPorConcepto,
        IReadOnlyList<string> Trazabilidad);

    private static string Ruta(string codigoColaborador, DateOnly fecha) =>
        $"/api/control-horas/depuraciones/{codigoColaborador}/{fecha:yyyy-MM-dd}";

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerDepuracionDelDia_Retorna200ConLaVistaCompleta_CuandoElConsumidorPersisteLaDepuracion()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        // Arrange: identificador unico por ejecucion y fecha fija (nunca DateTime.UtcNow). Tres
        // marcaciones: dos coinciden EXACTO con Entrada/Salida de la franja (Usada=true) y una no
        // coincide con ninguna (Usada=false) -- alcanza para demostrar que el endpoint devuelve la
        // derivacion real del generador, sin re-probar todas sus ramas (eso ya lo hace el unit test).
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 8);

        var evento = new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = new
            {
                Identificacion = "CC-444555666",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Depuracion"
            },
            NombreTurno = "[TEST] Turno Depuracion Query",
            Franjas = new object[]
            {
                new
                {
                    HoraInicioProgramada = "08:00:00",
                    HoraFinProgramada = "16:00:00",
                    DiaOffsetFin = 0,
                    Entrada = $"{fecha:yyyy-MM-dd}T08:00:00",
                    Salida = $"{fecha:yyyy-MM-dd}T16:00:00",
                    EsAnomala = false
                }
            },
            Marcaciones = new object[]
            {
                new { Timestamp = $"{fecha:yyyy-MM-dd}T08:00:00", Tipo = "ENTRADA" },
                new { Timestamp = $"{fecha:yyyy-MM-dd}T12:00:00", Tipo = "ALMUERZO" },
                new { Timestamp = $"{fecha:yyyy-MM-dd}T16:00:00", Tipo = "SALIDA" }
            },
            HorasDiscriminadas = new
            {
                HorasPorConcepto = new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m },
                Trazabilidad = new[] { "[TEST] regla aplicada" }
            }
        };

        // Act: publicar al bus interno -- el consumidor de ControlHoras persiste
        // depuracion_dia_recibida en el stream dc:{codigo}:{yyyyMMdd} de forma asincrona.
        await serviceBus.PublishAsync(TopicDiaDepurado, evento, Guid.CreateVersion7().ToString());

        // Act + Assert: reintentar el GET hasta que el consumidor persista la depuracion.
        var ruta = Ruta(codigoColaborador, fecha);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<DepuracionDelDiaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

        // Sin assert de NotBeNull: WaitUntilAsync devuelve un valor no nulo o lanza TimeoutException
        // ("el consumidor no persistio la depuracion dentro del timeout"), nunca null.

        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.Fecha.Should().Be(fecha);
        respuesta.IdentificacionColaborador.Should().Be("CC-444555666");
        respuesta.NombreColaborador.Should().Be("[TEST] Smoke Depuracion");
        respuesta.Estado.Should().Be(EstadoAsistenciaSmoke.Provisional);
        respuesta.Plan.Should().Be(PlanDelDiaSmoke.ConJornada);
        respuesta.NombreTurno.Should().Be("[TEST] Turno Depuracion Query");

        var franjaEsperada = new FranjaDepuradaSmoke(
            new TimeOnly(8, 0), new TimeOnly(16, 0), 0,
            fecha.ToDateTime(new TimeOnly(8, 0)), fecha.ToDateTime(new TimeOnly(16, 0)), false);
        respuesta.Franjas.Should().Equal(franjaEsperada);

        // Orden cronologico (contrato del evento) y Usada derivada por igualdad exacta contra
        // Entrada/Salida de la franja: la del almuerzo no coincide con ninguna, queda Usada=false.
        MarcacionDelDiaSmoke[] marcacionesEsperadas =
        [
            new(fecha.ToDateTime(new TimeOnly(8, 0)), "ENTRADA", true),
            new(fecha.ToDateTime(new TimeOnly(12, 0)), "ALMUERZO", false),
            new(fecha.ToDateTime(new TimeOnly(16, 0)), "SALIDA", true)
        ];
        respuesta.Marcaciones.Should().Equal(marcacionesEsperadas);

        respuesta.HorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);
        respuesta.Trazabilidad.Should().Equal("[TEST] regla aplicada");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerDepuracionDelDia_Retorna404SinBody_CuandoNoExisteStreamParaEseColaboradorYFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: codigoColaborador nuevo, nunca creado por ningun test -- ningun dato creo el
        // stream de ese dia.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 9);

        var response = await _client.GetAsync(Ruta(codigoColaborador, fecha), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerDepuracionDelDia_Retorna400_CuandoLaFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: formato DD-MM-YYYY en vez de yyyy-MM-dd.
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var response = await _client.GetAsync(
            $"/api/control-horas/depuraciones/{codigoColaborador}/09-06-2026", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
