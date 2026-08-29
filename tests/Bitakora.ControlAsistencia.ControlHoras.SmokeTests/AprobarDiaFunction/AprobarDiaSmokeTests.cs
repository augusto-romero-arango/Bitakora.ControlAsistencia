// Issue #489: smoke tests de POST control-horas/depuraciones/{codigoColaborador}/{fecha}:aprobar --
// el acto de aprobar el dia completo (Provisional -> Aprobado). Sin consumidores downstream
// (event-sourcing puro, CA-ADR-0030): el unico efecto verificable es dia_aprobado en mt_events; no
// hay topic de salida ni suscripcion smoke-tests que consumir (MEF-ADR-0039, el evento nace sin
// consumidor real).
//
// El arrange de los casos con precondicion Provisional publica DiaDepurado al topic dia-depurado
// (mismo mecanismo que ObtenerDepuracionDelDiaSmokeTests/RecibirDepuracionViaSbSmokeTests) y espera a
// que el consumidor persista depuracion_dia_recibida antes de invocar el POST bajo prueba -- esa
// persistencia es asincrona, un POST inmediato podria caer sobre un stream que aun no existe.
//
// Queda rojo hasta que el deploy publique la ruta en dev. El CI de PR no lo ejecuta (solo corre
// *.Tests); su veredicto real se lee despues del deploy.
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

    // Payload SIN conflicto de sede: la franja no trae CodigoSedeProgramada ni las marcaciones
    // CodigoSede -- DerivarSedeDeFranja no encuentra ninguna fuente, EnConflicto queda false.
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

    // Payload CON una franja en conflicto de sede: SEDE-01 programada vs SEDE-02 marcada en la
    // entrada -- dos fuentes de codigo distinto para la misma franja (DiaCalculadoAggregateRoot.
    // DerivarSedeDeFranja).
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

    // Arrange comun a los casos con precondicion Provisional: publica DiaDepurado y espera a que el
    // consumidor persista depuracion_dia_recibida antes de que el test invoque el POST bajo prueba.
    private async Task ArrangeDiaProvisionalAsync(string streamId, string codigoColaborador, object diaDepurado)
    {
        await serviceBus.PublishAsync(TopicDiaDepurado, diaDepurado, Guid.CreateVersion7().ToString());

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

    // CA-1: dia Provisional sin conflictos de sede, sin decisiones -> 202 + dia_aprobado con
    // SedesDecididas vacia.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna202YPersisteDiaAprobado_CuandoElDiaNoTieneConflictosDeSede()
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

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

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

    // CA-2: franja en conflicto con decision valida -> 202 + dia_aprobado carga la candidata completa
    // (codigo + nombre + CC del estampado de la MARCACION, que es la fuente elegida en este arrange).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna202YPersisteLaCandidataCompleta_CuandoLaDecisionResuelveElConflicto()
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

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

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

    // CA-3: quedan franjas en conflicto sin decidir (payload vacio) -> 409, sin evento persistido.
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

    // CA-4: CodigoSede decidido que no esta entre las candidatas de la franja en conflicto -> 409.
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

    // CA-5: decision para una franja SIN conflicto -> 409 (el payload afirma algo que el expediente
    // contradice).
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

    // CA-6: aprobar un dia ya Aprobado -> 409, re-aprobar es error (las aprobaciones son definitivas).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna409_CuandoElDiaYaFueAprobado()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 6);
        var payload = new { decisiones = Array.Empty<object>() };

        var primeraRespuesta = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);
        primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var segundaRespuesta = await _client.PostAsJsonAsync(RutaAprobar(codigoColaborador, fecha), payload, ct);

        segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-7 (aval del vacio): aprobar un dia SIN stream previo es valido -- crea el stream con
    // dia_aprobado como primer evento. Sin arrange de ServiceBus: el punto de esta CA es precisamente
    // que nunca llego ninguna DepuracionDiaRecibida.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AprobarDia_Retorna202YCreaElStream_CuandoElDiaNoTieneStreamPrevio()
    {
        Assert.SkipWhen(!postgres.IsConfigured,
            postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 9, 7);
        var streamId = ComputarStreamId(codigoColaborador, fecha);

        var response = await _client.PostAsJsonAsync(
            RutaAprobar(codigoColaborador, fecha), new { decisiones = Array.Empty<object>() }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);

        existe.Should().BeTrue(
            $"el aval del vacio deberia crear el stream {streamId} con {TipoEventoDiaAprobado} como primer evento");
    }

    // CA-7 contracara: dia sin stream + payload con decisiones -> 409 (el expediente vacio no tiene
    // ninguna franja que decidir, mismo caso que CA-5).
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

    // CA-8 (guarda minima): una DepuracionDiaRecibida (foto tardia) que llega DESPUES de aprobar no
    // agrega evento nuevo al stream -- el timeout corto le da tiempo al consumidor de procesar el
    // mensaje antes de afirmar la ausencia (ExisteEventoAsync agota el timeout y devuelve false si la
    // condicion nunca se cumple, no lanza).
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var aprobado = await postgres.ExisteEventoAsync(
            SchemaControlHoras, streamId, TipoEventoDiaAprobado, Timeout,
            campoJson: "CodigoColaborador", valorJson: codigoColaborador);
        aprobado.Should().BeTrue(
            $"el arrange depende de que {TipoEventoDiaAprobado} exista antes de enviar la foto tardia");

        await serviceBus.PublishAsync(
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
