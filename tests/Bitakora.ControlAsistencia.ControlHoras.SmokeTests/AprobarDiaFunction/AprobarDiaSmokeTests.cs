// Sin consumidores downstream (CA-ADR-0030): el unico efecto verificable es dia_aprobado en
// mt_events; no hay topic de salida que consumir.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AprobarDiaFunction;

public class AprobarDiaSmokeTests(ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicDiaDepurado = "dia-depurado";
    private const string SchemaControlHoras = "control_horas";
    private const string TipoEventoDiaAprobado = "dia_aprobado";
    private const string TipoEventoDepuracionDiaRecibida = "depuracion_dia_recibida";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // En sync con DiaCalculadoAggregateRoot.ComputarStreamId (prefijo "dc", CA-ADR-0031).
    private static string ComputarStreamId(string codigoColaborador, DateOnly fecha) =>
        $"dc:{codigoColaborador}:{fecha:yyyyMMdd}";

    private static string RutaAprobar(string codigoColaborador, DateOnly fecha) =>
        $"/api/control-horas/depuraciones/{codigoColaborador}/{fecha:yyyy-MM-dd}:aprobar";

    // Sin conflicto de sede: ni la franja ni las marcaciones traen sede.
    private static object CrearDiaDepuradoSinConflicto(
        string codigoColaborador, DateOnly fecha, TimeOnly horaInicio, TimeOnly horaFin) => new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = new
            {
                Identificacion = "CC-100200300",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Aprobar"
            },
            NombreTurno = "[TEST] Turno Aprobar",
            Franjas = new object[]
        {
            new
            {
                HoraInicioProgramada = horaInicio.ToString("HH:mm:ss"),
                HoraFinProgramada = horaFin.ToString("HH:mm:ss"),
                DiaOffsetFin = 0,
                Entrada = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}",
                Salida = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}",
                EsAnomala = false
            }
        },
            Marcaciones = new object[]
        {
            new { Timestamp = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}", Tipo = "ENTRADA" },
            new { Timestamp = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}", Tipo = "SALIDA" }
        },
            HorasDiscriminadas = new
            {
                HorasPorConcepto = new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m },
                Trazabilidad = Array.Empty<string>()
            }
        };

    // Conflicto de sede: SEDE-01 programada vs SEDE-02 marcada en la entrada.
    private static object CrearDiaDepuradoConConflicto(
        string codigoColaborador, DateOnly fecha, TimeOnly horaInicio, TimeOnly horaFin) => new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = new
            {
                Identificacion = "CC-100200301",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Aprobar Conflicto"
            },
            NombreTurno = "[TEST] Turno Aprobar Conflicto",
            Franjas = new object[]
        {
            new
            {
                HoraInicioProgramada = horaInicio.ToString("HH:mm:ss"),
                HoraFinProgramada = horaFin.ToString("HH:mm:ss"),
                DiaOffsetFin = 0,
                Entrada = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}",
                Salida = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}",
                EsAnomala = false,
                CodigoSedeProgramada = "SEDE-01",
                NombreSedeProgramada = "[TEST] Sede Principal",
                CentroDeCostosProgramado = "CC-100"
            }
        },
            Marcaciones = new object[]
        {
            new
            {
                Timestamp = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}",
                Tipo = "ENTRADA",
                CodigoSede = "SEDE-02",
                NombreSede = "[TEST] Sede Norte",
                CentroDeCostos = "CC-200"
            },
            new { Timestamp = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}", Tipo = "SALIDA" }
        },
            HorasDiscriminadas = new
            {
                HorasPorConcepto = new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m },
                Trazabilidad = Array.Empty<string>()
            }
        };

    // El consumidor persiste depuracion_dia_recibida de forma asincrona: un POST inmediato podria
    // caer sobre un stream que aun no existe.
    private async Task ArrangeDiaProvisionalAsync(string streamId, string codigoColaborador, object diaDepurado)
    {
        await serviceBus.PublishAsync(codigoColaborador, TopicDiaDepurado, diaDepurado, Guid.CreateVersion7().ToString());

        var persistido = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDepuracionDiaRecibida, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        persistido.Should().BeTrue(
            $"el arrange de este smoke test depende de que el consumidor persista {TipoEventoDepuracionDiaRecibida} en {streamId}");
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
    public async Task AprobarDia_Retorna204YPersisteDiaAprobado_CuandoElDiaNoTieneConflictosDeSede()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 1);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(8, 0);
        var horaFin = new TimeOnly(16, 0);

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador,
            CrearDiaDepuradoSinConflicto(codigoColaborador, fecha, horaInicio, horaFin));

        var payload = new { decisiones = Array.Empty<object>() };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeTrue(
            $"el evento {TipoEventoDiaAprobado} deberia existir en el stream {streamId} tras aprobar");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador, TimeSpan.FromSeconds(5));

        eventoPersistido.GetProperty("SedesDecididas").GetArrayLength().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna204YPersisteLaCandidataCompleta_CuandoLaDecisionResuelveElConflicto()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 2);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(6, 0);
        var horaFin = new TimeOnly(14, 0);

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador,
            CrearDiaDepuradoConConflicto(codigoColaborador, fecha, horaInicio, horaFin));

        var payload = new
        {
            decisiones = new[]
            {
                new { horaInicioProgramada = horaInicio.ToString("HH:mm:ss"), codigoSede = "SEDE-02" }
            }
        };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador, Timeout);

        var sedesDecididas = eventoPersistido.GetProperty("SedesDecididas");
        sedesDecididas.GetArrayLength().Should().Be(1);

        var sedeDecidida = sedesDecididas[0];
        sedeDecidida.GetProperty("CodigoSede").GetString().Should().Be("SEDE-02");
        sedeDecidida.GetProperty("NombreSede").GetString().Should().Be("[TEST] Sede Norte");
        sedeDecidida.GetProperty("CentroDeCostos").GetString().Should().Be("CC-200");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoHayConflictosDeSedeSinDecidir()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 3);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(6, 0);
        var horaFin = new TimeOnly(14, 0);

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador,
            CrearDiaDepuradoConConflicto(codigoColaborador, fecha, horaInicio, horaFin));

        var payload = new { decisiones = Array.Empty<object>() };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, TimeSpan.FromSeconds(5),
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeFalse(
            $"un 409 no deberia persistir {TipoEventoDiaAprobado} en el stream {streamId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoLaFotoTraeMarcacionesAmbiguasSinResolver()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 10);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(8, 0);
        var horaFin = new TimeOnly(16, 0);

        var diaDepurado = new
        {
            CodigoColaborador = codigoColaborador,
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Colaborador = new
            {
                Identificacion = "CC-100200302",
                CodigoColaborador = codigoColaborador,
                NombreCompleto = "[TEST] Smoke Aprobar Ambigua"
            },
            NombreTurno = "[TEST] Turno Aprobar Ambigua",
            Franjas = new object[]
            {
                new
                {
                    HoraInicioProgramada = horaInicio.ToString("HH:mm:ss"),
                    HoraFinProgramada = horaFin.ToString("HH:mm:ss"),
                    DiaOffsetFin = 0,
                    Entrada = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}",
                    Salida = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}",
                    EsAnomala = false
                }
            },
            Marcaciones = new object[]
            {
                new { Timestamp = $"{fecha:yyyy-MM-dd}T{horaInicio:HH:mm:ss}", Tipo = "ENTRADA", Pertenencia = "DelTurno" },
                new { Timestamp = $"{fecha:yyyy-MM-dd}T{horaFin:HH:mm:ss}", Tipo = "SALIDA", Pertenencia = "Ambigua" }
            },
            HorasDiscriminadas = new
            {
                HorasPorConcepto = new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m },
                Trazabilidad = Array.Empty<string>()
            }
        };

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador, diaDepurado);

        var payload = new { decisiones = Array.Empty<object>() };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, TimeSpan.FromSeconds(5),
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeFalse(
            $"un 409 no deberia persistir {TipoEventoDiaAprobado} en el stream {streamId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoElCodigoSedeDecididoNoEsCandidata()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 4);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(6, 0);
        var horaFin = new TimeOnly(14, 0);

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador,
            CrearDiaDepuradoConConflicto(codigoColaborador, fecha, horaInicio, horaFin));

        var payload = new
        {
            decisiones = new[]
            {
                new { horaInicioProgramada = horaInicio.ToString("HH:mm:ss"), codigoSede = "SEDE-99" }
            }
        };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoLaDecisionEsParaUnaFranjaSinConflicto()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 5);
        var streamId = ComputarStreamId(codigoColaborador, fecha);
        var horaInicio = new TimeOnly(8, 0);
        var horaFin = new TimeOnly(16, 0);

        await ArrangeDiaProvisionalAsync(streamId, codigoColaborador,
            CrearDiaDepuradoSinConflicto(codigoColaborador, fecha, horaInicio, horaFin));

        var payload = new
        {
            decisiones = new[]
            {
                new { horaInicioProgramada = horaInicio.ToString("HH:mm:ss"), codigoSede = "SEDE-01" }
            }
        };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Las aprobaciones son definitivas: re-aprobar es error.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoElDiaYaFueAprobado()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 6);
        var payload = new { decisiones = Array.Empty<object>() };

        var primeraRespuesta = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var segundaRespuesta = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Aval del vacio: sin arrange de ServiceBus a proposito, nunca llego una DepuracionDiaRecibida.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna204YCreaElStream_CuandoElDiaNoTieneStreamPrevio()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 7);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        var response = await _client.PostAsJsonAsync(
            RutaAprobar(codigoColaborador, fecha), new { decisiones = Array.Empty<object>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeTrue(
            $"el aval del vacio deberia crear el stream {streamId} con {TipoEventoDiaAprobado} como primer evento");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoElDiaNoTieneStreamYElPayloadTraeDecisiones()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 8);

        var payload = new
        {
            decisiones = new[]
            {
                new { horaInicioProgramada = "06:00:00", codigoSede = "SEDE-01" }
            }
        };
        var response = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ExisteEventoAsync agota el timeout y devuelve false sin lanzar: los 10 s le dan tiempo al
    // consumidor de procesar la foto tardia antes de afirmar la ausencia.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_IgnoraFotoTardia_CuandoDepuracionDiaRecibidaLlegaTrasLaAprobacion()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 9);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        var response = await _client.PostAsJsonAsync(
            RutaAprobar(codigoColaborador, fecha), new { decisiones = Array.Empty<object>() }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var aprobado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);
        aprobado.Should().BeTrue(
            $"el arrange depende de que {TipoEventoDiaAprobado} exista antes de enviar la foto tardia");

        await serviceBus.PublishAsync(
            codigoColaborador,
            TopicDiaDepurado,
            CrearDiaDepuradoSinConflicto(codigoColaborador, fecha, new TimeOnly(8, 0), new TimeOnly(16, 0)),
            Guid.CreateVersion7().ToString());

        var depuracionTardia = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDepuracionDiaRecibida, TimeSpan.FromSeconds(10),
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        depuracionTardia.Should().BeFalse(
            $"CA-8: la guarda minima descarta en silencio la foto tardia -- {TipoEventoDepuracionDiaRecibida} no deberia existir en {streamId} tras la aprobacion");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna400_CuandoLaFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();

        var response = await _client.PostAsJsonAsync(
            $"/api/control-horas/depuraciones/{codigoColaborador}/07-09-2026:aprobar",
            new { decisiones = Array.Empty<object>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
