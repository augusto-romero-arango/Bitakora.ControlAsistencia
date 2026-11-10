// Sin proyeccion materializada: DiaCalculadoAggregateRoot genera la vista al vuelo sobre el aggregate
// hidratado. El arrange publica DiaDepurado y la persistencia es asincrona: un GET inmediato puede dar 404
// legitimo, asi que el caso de exito va en Polling y agotar el timeout es un fallo real, no un skip.
// Los enums locales replican el ORDEN de produccion porque STJ los serializa como entero.
// Las derivaciones del generador las cubre su unit test; aqui basta un camino feliz black-box.
// El scoping por tenant no es observable con un solo tenant: lo cubre el test de composicion.
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
    private const string TopicAusenciaDiariaProgramada = "ausencia-diaria-programada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase y las formas locales son PascalCase.
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
        SinProgramar,
        Ausencia
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
        IReadOnlyList<string> Trazabilidad,
        string? MotivoAusencia);

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

        // Dos marcaciones coinciden exacto con Entrada/Salida de la franja (Usada=true) y una con ninguna
        // (Usada=false): alcanza para ver la derivacion real sin re-probar sus ramas.
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

        await serviceBus.PublishAsync(codigoColaborador, TopicDiaDepurado, evento, Guid.CreateVersion7().ToString());

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

        // Orden cronologico y Usada por igualdad exacta contra Entrada/Salida: la del almuerzo queda false.
        MarcacionDelDiaSmoke[] marcacionesEsperadas =
        [
            new(fecha.ToDateTime(new TimeOnly(8, 0)), "ENTRADA", true),
            new(fecha.ToDateTime(new TimeOnly(12, 0)), "ALMUERZO", false),
            new(fecha.ToDateTime(new TimeOnly(16, 0)), "SALIDA", true)
        ];
        respuesta.Marcaciones.Should().Equal(marcacionesEsperadas);

        respuesta.HorasPorConcepto.Should().ContainKey("OrdinariaDiurna").WhoseValue.Should().Be(8.00m);
        respuesta.Trazabilidad.Should().Equal("[TEST] regla aplicada");
        respuesta.MotivoAusencia.Should().BeNull();
    }

    // El Act publica el payload plano que emite Programacion al registrar la ausencia: este proyecto
    // solo alcanza el host de ControlHoras.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerDepuracionDelDia_Retorna200ConPlanAusenciaYMotivo_CuandoSeRegistraUnaAusenciaDeUnDia()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 16);
        var ausenciaId = Guid.CreateVersion7();
        const string motivo = "Vacaciones";

        await serviceBus.PublishAsync(codigoColaborador, TopicAusenciaDiariaProgramada, new
        {
            AusenciaId = ausenciaId,
            Colaborador = new
            {
                Identificacion = "CC-748748748",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Depuracion Ausencia"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Motivo = motivo
        }, ausenciaId.ToString());

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

        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.Fecha.Should().Be(fecha);
        respuesta.Estado.Should().Be(EstadoAsistenciaSmoke.Provisional);
        respuesta.Plan.Should().Be(PlanDelDiaSmoke.Ausencia);
        respuesta.MotivoAusencia.Should().Be(motivo);
        respuesta.Franjas.Should().BeEmpty();
        respuesta.HorasPorConcepto.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerDepuracionDelDia_Retorna404SinBody_CuandoNoExisteStreamParaEseColaboradorYFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // Codigo nunca creado por ningun test: ningun dato creo el stream de ese dia.
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

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd.
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var response = await _client.GetAsync(
            $"/api/control-horas/depuraciones/{codigoColaborador}/09-06-2026", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
