using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ResolverAmbiguedadDeMarcacionesFunction;

public class ResolverAmbiguedadDeMarcacionesSmokeTests(ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaMarcaciones = "/api/control-horas/marcaciones";
    private const string TopicProgramacionEntrada = ProgramacionEnLote.Topic;
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEventoTurnoDiarioAsignado = "turno_diario_asignado";
    private const string TipoEventoCoberturaVecinaActualizada = "cobertura_vecina_actualizada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record FranjaDepuradaSmoke(DateTime? Entrada, DateTime? Salida, bool EsAnomala);

    private sealed record MarcacionSmoke(DateTime Timestamp, JsonElement? Pertenencia);

    private sealed record DepuracionDelDiaSmoke(
        IReadOnlyList<FranjaDepuradaSmoke> Franjas,
        IReadOnlyList<MarcacionSmoke> Marcaciones);

    private static bool EsPertenencia(MarcacionSmoke marcacion, string nombre, int valor) =>
        marcacion.Pertenencia is { } p
        && (p.ValueKind == JsonValueKind.String
            ? string.Equals(p.GetString(), nombre, StringComparison.OrdinalIgnoreCase)
            : p.ValueKind == JsonValueKind.Number && p.GetInt32() == valor);

    private static bool EsAmbigua(MarcacionSmoke m) => EsPertenencia(m, "Ambigua", 2);

    private static bool EsDelTurno(MarcacionSmoke m) => EsPertenencia(m, "DelTurno", 0);

    private static bool EsFueraDeCobertura(MarcacionSmoke m) => EsPertenencia(m, "FueraDeCobertura", 1);

    private static string RutaDepuracion(string codigoColaborador, DateOnly fecha) =>
        $"/api/control-horas/depuraciones/{codigoColaborador}/{fecha:yyyy-MM-dd}";

    private static string RutaResolver(string codigoColaborador, DateOnly fecha) =>
        $"{RutaDepuracion(codigoColaborador, fecha)}:resolver-ambiguedad";

    private static object Turno(
        string codigoColaborador, DateOnly fecha, string horaInicio, string horaFin, int diaOffsetFin)
    {
        var franja = new
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetFin = diaOffsetFin,
            Descansos = Array.Empty<object>(),
            Extras = Array.Empty<object>()
        };

        return new
        {
            SolicitudId = Guid.CreateVersion7(),
            Colaborador = new
            {
                Identificacion = "CC-777666444",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Resolver Ambiguedad"
            },
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
            DetalleTurno = new { Nombre = "[TEST] Turno Resolver Ambiguedad", FranjasOrdinarias = new[] { franja } }
        };
    }

    private async Task<DepuracionDelDiaSmoke?> ObtenerDepuracionAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        var response = await _client.GetAsync(RutaDepuracion(codigoColaborador, fecha), ct);
        if (response.StatusCode != HttpStatusCode.OK)
            return null;

        return await response.Content.ReadFromJsonAsync<DepuracionDelDiaSmoke>(JsonOptions, cancellationToken: ct);
    }

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
    public async Task ResolverAmbiguedad_AsignaLaMarcacionALaVispera_CuandoCaeEnElSolapeDeDosTurnos()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var vispera = new DateOnly(2026, 10, 20);
        var dia = vispera.AddDays(1);
        var entradaVispera = vispera.ToDateTime(new TimeOnly(16, 55));
        var enDisputa = dia.ToDateTime(new TimeOnly(4, 30));
        var entradaDia = dia.ToDateTime(new TimeOnly(6, 20));
        var salidaDia = dia.ToDateTime(new TimeOnly(14, 5));

        await serviceBus.PublishAsync(codigoColaborador, TopicProgramacionEntrada,
            Turno(codigoColaborador, vispera, "17:00:00", "01:00:00", 1), Guid.CreateVersion7().ToString());
        await serviceBus.PublishAsync(codigoColaborador, TopicProgramacionEntrada,
            Turno(codigoColaborador, dia, "06:00:00", "14:00:00", 0), Guid.CreateVersion7().ToString());

        foreach (var tipoEvento in new[] { TipoEventoTurnoDiarioAsignado, TipoEventoCoberturaVecinaActualizada })
        {
            foreach (var fecha in new[] { vispera, dia })
            {
                var streamId = $"cd:{codigoColaborador}:{fecha:yyyyMMdd}";
                var existe = await postgres.ExisteEventoAsync(SchemaControlHoras, streamId, tipoEvento, Timeout);
                existe.Should().BeTrue(
                    "el arrange debe esperar {0} en el stream {1} antes de registrar marcaciones (arrange incompleto)",
                    tipoEvento, streamId);
            }
        }

        foreach (var momento in new[] { entradaVispera, enDisputa, entradaDia, salidaDia })
        {
            var response = await _client.PostAsJsonAsync(RutaMarcaciones, new
            {
                codigoColaborador,
                timestamp = momento.ToString("yyyy-MM-ddTHH:mm:ss") + "Z",
                tipoMarcacion = "ENTRADA",
                dispositivoId = "[TEST] DEV-SMOKE-RESOLVER"
            }, ct);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        DepuracionDelDiaSmoke? depuracionDia = null;
        var arreglado = await Polling.WaitUntilTrueAsync(async () =>
        {
            depuracionDia = await ObtenerDepuracionAsync(codigoColaborador, dia, ct);
            return depuracionDia is not null
                && depuracionDia.Marcaciones.Any(m => m.Timestamp == enDisputa && EsAmbigua(m))
                && depuracionDia.Marcaciones.Any(m => m.Timestamp == entradaDia)
                && depuracionDia.Marcaciones.Any(m => m.Timestamp == salidaDia);
        }, Timeout);

        arreglado.Should().BeTrue(
            "el arrange debe dejar la marcacion 04:30 ambigua en el dia, con las demas marcaciones reflejadas");

        var resolver = await _client.PostAsJsonAsync(RutaResolver(codigoColaborador, dia), new
        {
            marcaciones = new[]
            {
                new { marcacion = enDisputa.ToString("yyyy-MM-ddTHH:mm"), fecha = vispera.ToString("yyyy-MM-dd") }
            }
        }, ct);

        resolver.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await resolver.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        DepuracionDelDiaSmoke? depuracionVispera = null;
        var resuelta = await Polling.WaitUntilTrueAsync(async () =>
        {
            depuracionVispera = await ObtenerDepuracionAsync(codigoColaborador, vispera, ct);
            depuracionDia = await ObtenerDepuracionAsync(codigoColaborador, dia, ct);
            return depuracionVispera is not null
                && depuracionDia is not null
                && depuracionVispera.Marcaciones.Any(m => m.Timestamp == enDisputa && EsDelTurno(m))
                && depuracionDia.Marcaciones.Any(m => m.Timestamp == enDisputa && EsFueraDeCobertura(m));
        }, Timeout);

        resuelta.Should().BeTrue(
            "la 04:30 debe quedar DelTurno en la vispera y FueraDeCobertura en el dia tras resolver la ambiguedad");

        depuracionVispera!.Franjas.Should().ContainSingle()
            .Which.Should().Match<FranjaDepuradaSmoke>(f =>
                f.Entrada == entradaVispera && f.Salida == enDisputa && !f.EsAnomala);
        depuracionDia!.Franjas.Should().Contain(f => f.Entrada == entradaDia && f.Salida == salidaDia);
        depuracionVispera.Marcaciones.Should().NotContain(m => EsAmbigua(m));
        depuracionDia.Marcaciones.Should().NotContain(m => EsAmbigua(m));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ResolverAmbiguedad_RetornaNotFound_CuandoElDiaNoTieneDepuracion()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var dia = new DateOnly(2026, 10, 21);

        var response = await _client.PostAsJsonAsync(RutaResolver(codigoColaborador, dia), new
        {
            marcaciones = new[] { new { marcacion = "2026-10-21T04:30", fecha = "2026-10-20" } }
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ResolverAmbiguedad_RetornaBadRequest_CuandoLaMarcacionDelBodyEstaMalFormada()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var dia = new DateOnly(2026, 10, 21);

        var response = await _client.PostAsJsonAsync(RutaResolver(codigoColaborador, dia), new
        {
            marcaciones = new[] { new { marcacion = "no-es-una-marcacion", fecha = "2026-10-20" } }
        }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
