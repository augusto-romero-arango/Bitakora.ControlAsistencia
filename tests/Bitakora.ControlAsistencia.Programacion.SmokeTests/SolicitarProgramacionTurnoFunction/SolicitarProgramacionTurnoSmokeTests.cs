using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoSmokeTests(ApiFixture api, ServiceBusFixture serviceBus, PostgresFixture postgres) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

    private const string Ruta = "/api/programacion/solicitudes";
    private const string RutaAliasTemporal = "/api/programacion/solicitudes-en-fechas";
    private const string TopicSalida = "programacion-turno-en-fechas-solicitada";
    private const string Suscripcion = "smoke-tests";
    private const string SchemaProgramacion = "programacion";
    private const string TipoEventoProgramacionSolicitada = "programacion_turno_solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private const string ClaveOrganizationMembershipId = "organization_membership_id";
    private static readonly string[] FechasPedidas = ["2026-04-15", "2026-04-16"];

    private static string CodigoColaboradorUnico() => Guid.CreateVersion7().ToString();

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PublicaUnEventoConSesionYFechas_CuandoLaSolicitudEsAceptada()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas"), ct);
        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = CodigoColaboradorUnico();

        var response = await _client.PostAsJsonAsync(Ruta, PayloadSolicitud(solicitudId, turnoId, FechasPedidas, codigoColaborador: codigoColaborador), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var (evento, sessionId) = await serviceBus.WaitForMessageConSesionAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        sessionId.Should().Be(codigoColaborador);
        evento.Fechas.Should().BeEquivalentTo(new[] { new DateOnly(2026, 4, 15), new DateOnly(2026, 4, 16) });

        var persistidos = await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada);
        persistidos.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoLaSolicitudYaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas"), ct);
        var payload = PayloadSolicitud(Guid.CreateVersion7(), turnoId, FechasPedidas);

        var primera = await _client.PostAsJsonAsync(Ruta, payload, ct);
        primera.StatusCode.Should().Be(HttpStatusCode.Created);

        var segunda = await _client.PostAsJsonAsync(Ruta, payload, ct);
        segunda.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna404_CuandoElTurnoNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), Guid.CreateVersion7(), FechasPedidas), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoFechasEstaVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), Guid.CreateVersion7(), []), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record FranjaMinima(SedeMinima? Sede);
    private sealed record TurnoMinimo(IReadOnlyList<FranjaMinima> FranjasOrdinarias);
    private sealed record SolicitudMinima(TurnoMinimo DetalleTurno, SedeMinima? Sede);
    private sealed record FechaRespetadaMinima(string Fecha, string Motivo);
    private sealed record RespuestaSolicitudMinima(IReadOnlyList<FechaRespetadaMinima> FechasRespetadas);
    private sealed record SolicitudFechasMinima(IReadOnlyList<DateOnly> Fechas);

    private static readonly JsonSerializerOptions OpcionesRespuesta = new(JsonSerializerDefaults.Web);

    private static object TurnoSimplePayload(Guid turnoId, string nombreBase) => new
    {
        turnoId,
        nombre = $"{nombreBase} {turnoId}",
        ordinarias = new[]
        {
            new
            {
                inicio = "08:00:00",
                fin = "16:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    private static object TurnoConFranjasMixtasPayload(Guid turnoId, string sedeId, string sedeNombre) => new
    {
        turnoId,
        nombre = $"[TEST] Turno Mixto Cascada En Fechas {turnoId}",
        ordinarias = new object[]
        {
            new
            {
                inicio = "06:00:00",
                fin = "10:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>(),
                sede = new { id = sedeId, nombre = sedeNombre }
            },
            new
            {
                inicio = "14:00:00",
                fin = "18:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    private static object TurnoConSedePrearmadaPayload(Guid turnoId, DetalleSede sede) => new
    {
        turnoId,
        nombre = $"[TEST] Turno Con Sede Prearmada En Fechas {turnoId}",
        ordinarias = new[]
        {
            new
            {
                inicio = "08:00:00",
                fin = "16:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>(),
                sede = new { id = sede.Id, nombre = sede.Nombre }
            }
        }
    };

    private async Task<Guid> CrearTurnoDePayloadAsync(Func<Guid, object> payload, CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync("/api/programacion/turnos", payload(turnoId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return turnoId;
    }

    private static object ColaboradorPayload(string codigoColaborador) => new
    {
        identificacion = "CC-745745745",
        codigoColaborador,
        nombreCompleto = "[TEST] Smoke En Fechas"
    };

    private static object PayloadSolicitud(
        Guid solicitudId, Guid turnoId, string[] fechas, object? sede = null, Guid? jornadaId = null,
        string? codigoColaborador = null) => new
        {
            id = solicitudId,
            turnoId,
            colaborador = ColaboradorPayload(codigoColaborador ?? CodigoColaboradorUnico()),
            fechas,
            sede,
            jornadaId
        };

    private static object AusenciaPayload(string fechaInicio, string fechaFin) => new
    {
        id = Guid.CreateVersion7(),
        identificacion = "CC-745745745",
        nombreCompleto = "[TEST] Smoke En Fechas",
        fechaInicio,
        fechaFin,
        motivo = "Vacaciones"
    };

    private async Task<SolicitudMinima> LeerPersistidoAsync(Guid solicitudId)
    {
        var streamId = solicitudId.ToString();
        var json = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoProgramacionSolicitada,
            campoJson: "Id", valorJson: streamId, Timeout);
        var persistido = json.Deserialize<SolicitudMinima>(EventoPersistido.OpcionesLectura);
        persistido.Should().NotBeNull();
        return persistido!;
    }

    private static void SaltarSiNoHayServiceBus(ServiceBusFixture serviceBus) =>
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

    private static void SaltarSiNoHayPostgres(PostgresFixture postgres) =>
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_EstampaElMembershipEnElEventoYEnElMensaje_CuandoLaSolicitudEsAceptada()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Membership"), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta, PayloadSolicitud(solicitudId, turnoId, ["2026-10-01"]), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var membershipPersistido = await postgres.ObtenerHeaderDeEventoAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada,
            ClaveOrganizationMembershipId, Timeout);
        membershipPersistido.Should().Be(api.OrganizationMembershipId);

        var (_, propiedades) = await serviceBus.WaitForMessageConPropiedadesAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        propiedades.Should().ContainKey(ClaveOrganizationMembershipId)
            .WhoseValue.ToString().Should().Be(api.OrganizationMembershipId);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PublicaElEventoConLaSede_CuandoLaSolicitudIncluyeSede()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Sede"), ct);
        var solicitudId = Guid.CreateVersion7();
        var sedeEsperada = new DetalleSede("SEDE-01", "[TEST] Sede Principal");

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-05-01"], new { id = sedeEsperada.Id, nombre = sedeEsperada.Nombre }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().Be(sedeEsperada);
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeEsperada);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ConservaLaSedeDeLaFranjaYAplicaLaSedePorDefecto_CuandoElTurnoTraeFranjasMixtas()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var sedePrincipal = new DetalleSede("SEDE-01", "[TEST] Sede Principal");
        var sedeSuba = new DetalleSede("SEDE-SUBA", "[TEST] Suba");
        var turnoId = await CrearTurnoDePayloadAsync(
            id => TurnoConFranjasMixtasPayload(id, sedeSuba.Id, sedeSuba.Nombre), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-06-01"], new { id = sedePrincipal.Id, nombre = sedePrincipal.Nombre }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().Be(sedePrincipal);
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(2);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeSuba);
        evento.DetalleTurno.FranjasOrdinarias[1].Sede.Should().Be(sedePrincipal);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ConservaLaSedeDelCatalogo_CuandoLaSolicitudNoIncluyeSede()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var sedeCatalogo = new DetalleSede("SEDE-CENTRO", "[TEST] Centro");
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoConSedePrearmadaPayload(id, sedeCatalogo), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta, PayloadSolicitud(solicitudId, turnoId, ["2026-06-02"]), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().BeNull();
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeCatalogo);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PropagaElCentroDeCostosEnLaSedeYEnLaFranja_CuandoLaSolicitudLoIncluye()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas CC"), ct);
        var solicitudId = Guid.CreateVersion7();
        var sedeEsperada = new DetalleSede("SEDE-CC-01", "[TEST] Sede Con Costeo", "CC-100-VENTAS");

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-07-01"],
                new { id = sedeEsperada.Id, nombre = sedeEsperada.Nombre, centroDeCostos = sedeEsperada.CentroDeCostos }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

        evento.Sede.Should().Be(sedeEsperada);
        evento.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        evento.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeEsperada);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_NormalizaCentroDeCostosANull_CuandoVieneVacioOEnBlanco()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas CC Normalizacion"), ct);
        var sedeEsperada = new DetalleSede("SEDE-CC-02", "[TEST] Sede Sin Costeo Real");

        foreach (var centroDeCostosCrudo in new[] { "", "   " })
        {
            var solicitudId = Guid.CreateVersion7();
            var response = await _client.PostAsJsonAsync(Ruta,
                PayloadSolicitud(solicitudId, turnoId, ["2026-07-02"],
                    new { id = sedeEsperada.Id, nombre = sedeEsperada.Nombre, centroDeCostos = centroDeCostosCrudo }), ct);
            response.StatusCode.Should().Be(HttpStatusCode.Created);

            var evento = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
                TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);

            evento.Sede.Should().Be(sedeEsperada,
                "un centro de costos '{0}' debe llegar al bus como null", centroDeCostosCrudo);
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PersisteElCentroDeCostosEnLaSedeEfectiva_CuandoLaSolicitudLoIncluye()
    {
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas CC Persistencia"), ct);
        var solicitudId = Guid.CreateVersion7();
        var sedeMinima = new SedeMinima("SEDE-CC-03", "[TEST] Sede Persistencia CC", "CC-200-NOMINA");

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-07-03"],
                new { id = sedeMinima.Id, nombre = sedeMinima.Nombre, centroDeCostos = sedeMinima.CentroDeCostos }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var persistido = await LeerPersistidoAsync(solicitudId);

        persistido.Sede.Should().Be(sedeMinima);
        persistido.DetalleTurno.FranjasOrdinarias.Should().HaveCount(1);
        persistido.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeMinima);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_PersisteLaSedeEfectivaDeCadaFranja_CuandoElTurnoTraeFranjasMixtas()
    {
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        var sedeSuba = new SedeMinima("SEDE-SUBA", "[TEST] Suba");
        var sedePrincipal = new SedeMinima("SEDE-01", "[TEST] Sede Principal");
        var turnoId = await CrearTurnoDePayloadAsync(
            id => TurnoConFranjasMixtasPayload(id, sedeSuba.Id, sedeSuba.Nombre), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-06-03"], new { id = sedePrincipal.Id, nombre = sedePrincipal.Nombre }), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var persistido = await LeerPersistidoAsync(solicitudId);

        persistido.DetalleTurno.FranjasOrdinarias.Should().HaveCount(2);
        persistido.DetalleTurno.FranjasOrdinarias[0].Sede.Should().Be(sedeSuba);
        persistido.DetalleTurno.FranjasOrdinarias[1].Sede.Should().Be(sedePrincipal);
        persistido.Sede.Should().Be(sedePrincipal);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_EstampaLaJornada_CuandoTraeJornadaId()
    {
        SaltarSiNoHayPostgres(postgres);
        SaltarSiNoHayServiceBus(serviceBus);
        var firma = LimitesDePrueba.SolicitarProgramacionEnFechas;
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);

        var jornadaId = Guid.CreateVersion7();
        (await _client.PostAsJsonAsync("/api/programacion/jornadas", firma.PayloadCrear(jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Jornada"), ct);
        var solicitudId = Guid.CreateVersion7();

        (await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-11-02"], jornadaId: jornadaId), ct))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var mensaje = await serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        mensaje.Jornada.Should().NotBeNull();
        mensaje.Jornada!.JornadaId.Should().Be(jornadaId);
        mensaje.Jornada.HorasSemanalesEnMinutos.Should().Be(firma.HorasSemanalesEnMinutos);
        mensaje.Jornada.TopeDiarioEnMinutos.Should().Be(firma.TopeDiarioEnMinutos);
        mensaje.Jornada.MinimoDiarioEnMinutos.Should().Be(firma.MinimoDiarioEnMinutos);
        mensaje.Jornada.DiasDescansoPorSemana.Should().Be(firma.DiasDescansoPorSemana);
        (await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada)).Should().Be(1);
    }

    // Camino del MCP mientras se despliega por separado; se retira con el alias en #1387.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoEnFechas_Retorna201YPersisteLaSolicitud_CuandoSeLlamaPorElAliasTemporal()
    {
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke Alias En Fechas"), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(RutaAliasTemporal,
            PayloadSolicitud(solicitudId, turnoId, ["2026-12-03"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().BeNull();
        var cuerpo = await response.Content.ReadFromJsonAsync<RespuestaSolicitudMinima>(OpcionesRespuesta, ct);
        cuerpo!.FechasRespetadas.Should().BeEmpty();
        (await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada)).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna201ConListaVaciaDeRespetadas_CuandoElColaboradorNoTieneAusencias()
    {
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Sin Ausencias"), ct);
        var solicitudId = Guid.CreateVersion7();

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-12-01", "2026-12-02"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var cuerpo = await response.Content.ReadFromJsonAsync<RespuestaSolicitudMinima>(OpcionesRespuesta, ct);
        cuerpo!.FechasRespetadas.Should().BeEmpty();
        (await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada)).Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna201ConLaFechaRespetadaYPublicaSoloLasLibres_CuandoUnaFechaTieneAusencia()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Respeta Ausencia"), ct);

        var codigo = CodigoColaboradorUnico();
        var ausencia = await _client.PostAsJsonAsync(
            $"/api/programacion/colaboradores/{codigo}/ausencias", AusenciaPayload("2026-11-10", "2026-11-10"), ct);
        ausencia.StatusCode.Should().Be(HttpStatusCode.Created);

        var solicitudId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-11-09", "2026-11-10", "2026-11-11"], codigoColaborador: codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var cuerpo = await response.Content.ReadFromJsonAsync<RespuestaSolicitudMinima>(OpcionesRespuesta, ct);
        cuerpo!.FechasRespetadas.Should().ContainSingle()
            .Which.Should().Be(new FechaRespetadaMinima("2026-11-10", "Vacaciones"));

        var (evento, sessionId) = await serviceBus.WaitForMessageConSesionAsync<ProgramacionTurnoEnFechasSolicitada>(
            TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, Timeout);
        sessionId.Should().Be(codigo);
        evento.Fechas.Should().BeEquivalentTo(new[] { new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11) });

        var streamId = solicitudId.ToString();
        var json = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaProgramacion, streamId, TipoEventoProgramacionSolicitada,
            campoJson: "Id", valorJson: streamId, Timeout);
        json.Deserialize<SolicitudFechasMinima>(EventoPersistido.OpcionesLectura)!.Fechas.Should()
            .Equal(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna409SinPersistirNiPublicar_CuandoTodasLasFechasTienenAusencia()
    {
        SaltarSiNoHayServiceBus(serviceBus);
        SaltarSiNoHayPostgres(postgres);
        var ct = TestContext.Current.CancellationToken;
        await serviceBus.PurgeAsync(TopicSalida, Suscripcion);
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Smoke En Fechas Todas Ausentes"), ct);

        var codigo = CodigoColaboradorUnico();
        var ausencia = await _client.PostAsJsonAsync(
            $"/api/programacion/colaboradores/{codigo}/ausencias", AusenciaPayload("2026-11-16", "2026-11-17"), ct);
        ausencia.StatusCode.Should().Be(HttpStatusCode.Created);

        var solicitudId = Guid.CreateVersion7();
        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(solicitudId, turnoId, ["2026-11-16", "2026-11-17"], codigoColaborador: codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var mensaje = await response.Content.ReadAsStringAsync(ct);
        mensaje.Should().Contain("2026-11-16").And.Contain("Vacaciones");

        await Assert.ThrowsAsync<TimeoutException>(() =>
            serviceBus.WaitForMessageAsync<ProgramacionTurnoEnFechasSolicitada>(
                TopicSalida, Suscripcion, e => e.SolicitudId == solicitudId, TimeSpan.FromSeconds(3)));
        (await postgres.ContarEventosAsync(
            SchemaProgramacion, solicitudId.ToString(), TipoEventoProgramacionSolicitada)).Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoElTurnoDelCatalogoEstaRetirado()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => TurnoSimplePayload(id, "[TEST] Turno Retirado Para Solicitar En Fechas"), ct);

        var retirar = await _client.DeleteAsync($"/api/programacion/turnos/{turnoId}", ct);
        retirar.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), turnoId, ["2025-08-01"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna409_CuandoElTurnoEstaIncompleto()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoId = await CrearTurnoDePayloadAsync(id => new { turnoId = id, nombre = $"[TEST] Incompleto En Fechas {id}" }, ct);

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), turnoId, ["2025-08-01"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadAsStringAsync(ct);
        body.Should().Contain("no tiene franjas ordinarias");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.Empty, Guid.CreateVersion7(), ["2025-08-01"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoTurnoIdEsGuidVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), Guid.Empty, ["2025-08-01"]), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoColaboradorTieneCamposVacios()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = new { identificacion = "", codigoColaborador = "", nombreCompleto = "" },
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoColaboradorEsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            id = Guid.CreateVersion7(),
            turnoId = Guid.CreateVersion7(),
            colaborador = (object?)null,
            fechas = new[] { "2025-08-01" }
        };

        var response = await _client.PostAsJsonAsync(Ruta, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoSedeTieneIdVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), Guid.CreateVersion7(), ["2025-08-01"],
                new { id = "", nombre = "[TEST] Sede Principal" }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_Retorna400_CuandoSedeTieneNombreEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(Ruta,
            PayloadSolicitud(Guid.CreateVersion7(), Guid.CreateVersion7(), ["2025-08-01"],
                new { id = "SEDE-01", nombre = "   " }), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
