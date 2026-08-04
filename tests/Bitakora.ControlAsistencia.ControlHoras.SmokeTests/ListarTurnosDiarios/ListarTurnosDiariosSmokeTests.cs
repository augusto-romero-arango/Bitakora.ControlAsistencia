using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarTurnosDiarios;

// Issue #290: smoke tests de ListarTurnosDiarios, GET control-horas/turnos-diarios con desde/hasta
// obligatorios y empleadoId opcional. Segunda Function GET read-side del BC, sobre la MISMA vista
// TurnoDiarioView que ya materializa #289 -- se siembran datos publicando ProgramacionTurnoDiario-
// Solicitada al bus interno, exactamente el mismo mecanismo de ObtenerTurnoDiarioSmokeTests.
//
// La proyeccion tiene lifecycle Async (MEF-ADR-0034): el worker la materializa DESPUES de que
// ControlHoras persiste turno_diario_asignado. Los casos que dependen de datos sembrados envuelven
// la consulta en Polling.WaitUntilAsync/WaitUntilTrueAsync (timeout estandar 30s) -- si el timeout
// se agota es un fallo real (worker no desplegado o proyeccion sin registrar), nunca un skip.
//
// Formas locales DESACOPLADAS del DTO de produccion (Bitakora.ControlAsistencia.ControlHoras.
// ListarTurnosDiarios.ListaTurnosDiarios / ListarTurnosDiarios.RangoConsulta): el smoke test no
// referencia el proyecto de dominio (ControlHoras), solo PublicEvents/PrivateEvents (mismo patron
// que ObtenerTurnoDiarioSmokeTests). El limite de 31 dias se afirma como literal (desde + 30 dias),
// nunca leyendo RangoConsulta.CotaDias.
public class ListarTurnosDiariosSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record TurnoDiarioRespuestaSmoke(
        InformacionEmpleado Empleado,
        DateOnly Fecha,
        DetalleTurno DetalleTurno,
        Guid UltimaSolicitudId);

    private sealed record ListaTurnosDiariosSmoke(
        DateOnly DesdeAplicado,
        DateOnly HastaAplicado,
        bool RangoRecortado,
        IReadOnlyList<TurnoDiarioRespuestaSmoke> Turnos);

    private static string Ruta(DateOnly desde, DateOnly hasta, string? empleadoId = null)
    {
        var query = $"desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        if (empleadoId is not null)
            query += $"&empleadoId={Uri.EscapeDataString(empleadoId)}";

        return $"/api/control-horas/turnos-diarios?{query}";
    }

    private async Task PublicarTurnoAsync(
        Guid solicitudId, string empleadoId, DateOnly fecha, string nombreTurno)
    {
        var evento = new
        {
            SolicitudId = solicitudId,
            Empleado = new
            {
                EmpleadoId = empleadoId,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "777888999",
                Nombres = "[TEST] Smoke Listar",
                Apellidos = "[TEST] TurnosDiarios"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = nombreTurno,
                FranjasOrdinarias = new[]
                {
                    new
                    {
                        HoraInicio = "08:00:00",
                        HoraFin = "16:00:00",
                        DiaOffsetFin = 0,
                        Descansos = Array.Empty<object>(),
                        Extras = Array.Empty<object>(),
                        Descripcion = (string?)null
                    }
                },
                Descripcion = (string?)null
            }
        };

        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());
    }

    private async Task<bool> EsperarTurnoMaterializadoAsync(
        string empleadoId, DateOnly fecha, CancellationToken ct)
    {
        return await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(
                $"/api/control-horas/turnos-diarios/{empleadoId}/{fecha:yyyy-MM-dd}", ct);
            return response.StatusCode == HttpStatusCode.OK;
        }, Timeout);
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
    public async Task ListarTurnosDiarios_Retorna400_CuandoFaltaElParametroDesde()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            "/api/control-horas/turnos-diarios?hasta=2026-06-10", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_Retorna400_CuandoFaltaElParametroHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            "/api/control-horas/turnos-diarios?desde=2026-06-01", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_Retorna400_CuandoDesdeTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd, mismo precedente que ObtenerTurnoDiario (#289).
        var response = await _client.GetAsync(
            "/api/control-horas/turnos-diarios?desde=01-06-2026&hasta=2026-06-10", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_Retorna400_CuandoHastaEsAnteriorADesde()
    {
        var ct = TestContext.Current.CancellationToken;

        // Rango invertido: decision documentada en FunctionEndpoint.cs (400, no lista vacia).
        var desde = new DateOnly(2026, 6, 10);
        var hasta = new DateOnly(2026, 6, 5);

        var response = await _client.GetAsync(Ruta(desde, hasta), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_Retorna200ConListaVacia_CuandoNoHayTurnoAsignadoParaEseEmpleado()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: empleadoId nuevo, nunca creado por ningun test -- no puede tener turno asignado.
        var empleadoId = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 6, 1);
        var hasta = new DateOnly(2026, 6, 5);

        var response = await _client.GetAsync(Ruta(desde, hasta, empleadoId), ct);

        // Assert: CA-5 -- 200 con turnos: [], nunca 404. CA-4 -- rango dentro de la cota, sin recorte.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosDiariosSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.Turnos.Should().BeEmpty();
        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_Retorna200ConElTurnoDelEmpleado_CuandoSeConsultaPorRangoYEmpleado()
    {
        // Forma (b) del issue: "la programacion de Juan de esta semana" -- empleadoId + desde/hasta.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fechaTurno = new DateOnly(2026, 6, 10);
        var desde = new DateOnly(2026, 6, 1);
        var hasta = new DateOnly(2026, 6, 15); // 15 dias, dentro de la cota de 31

        await PublicarTurnoAsync(solicitudId, empleadoId, fechaTurno, "[TEST] Turno Listar Rango");

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la vista.
        var ruta = Ruta(desde, hasta, empleadoId);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosDiariosSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is { Turnos.Count: > 0 } ? body : null;
        }, Timeout);

        // Assert: CA-4 -- rango dentro de la cota, sin recorte. Filtrado por empleadoId (GUID unico
        // de esta ejecucion), asi que la lista contiene exactamente el turno sembrado, nunca por
        // posicion.
        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
        respuesta.Turnos.Should().ContainSingle();

        var turno = respuesta.Turnos[0];
        turno.Fecha.Should().Be(fechaTurno);
        turno.UltimaSolicitudId.Should().Be(solicitudId);
        turno.Empleado.EmpleadoId.Should().Be(empleadoId);
        turno.DetalleTurno.Nombre.Should().Be("[TEST] Turno Listar Rango");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_IncluyeElTurnoDelEmpleado_CuandoSeConsultaUnDiaConTodosLosEmpleados()
    {
        // Forma (c) del issue: "quien trabaja hoy y en que turno" -- desde == hasta, SIN empleadoId.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 20);

        await PublicarTurnoAsync(solicitudId, empleadoId, fecha, "[TEST] Turno Listar Dia");

        // Sin empleadoId: la lista puede incluir turnos de otras ejecuciones de esta misma suite
        // (sin cleanup, GUIDs unicos por corrida) -- se filtra siempre por SolicitudId, nunca por
        // posicion/indice.
        var ruta = Ruta(fecha, fecha);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosDiariosSmoke>(
                JsonOptions, cancellationToken: ct);
            var contieneElTurno = body?.Turnos.Any(t => t.UltimaSolicitudId == solicitudId) ?? false;
            return contieneElTurno ? body : null;
        }, Timeout);

        // Assert: CA-4 -- un dia (desde == hasta) esta muy por debajo de la cota, sin recorte.
        respuesta.DesdeAplicado.Should().Be(fecha);
        respuesta.HastaAplicado.Should().Be(fecha);
        respuesta.RangoRecortado.Should().BeFalse();

        var turno = respuesta.Turnos.Single(t => t.UltimaSolicitudId == solicitudId);
        turno.Empleado.EmpleadoId.Should().Be(empleadoId);
        turno.DetalleTurno.Nombre.Should().Be("[TEST] Turno Listar Dia");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosDiarios_RecortaHaciaAdelanteYExcluyeLoQueQuedaFuera_CuandoElRangoExcedeLaCotaDe31Dias()
    {
        // Forma (d) del issue: "la grilla del mes", pero pedida sobre un rango que excede la cota
        // (CA-3) -- se verifica que el recorte SI restringe la consulta, no solo que se declara en
        // el envelope.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var empleadoId = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 7, 1);
        var hastaSolicitado = new DateOnly(2026, 9, 30); // ~91 dias, muy por encima de la cota
        var hastaAplicadaEsperada = desde.AddDays(30); // CA-3: cota de 31 dias inclusive

        var solicitudDentro = Guid.CreateVersion7();
        var solicitudFuera = Guid.CreateVersion7();
        var fechaDentro = desde; // dentro del rango recortado
        var fechaFuera = hastaAplicadaEsperada.AddDays(5); // dentro de lo pedido, fuera del recorte

        await PublicarTurnoAsync(solicitudDentro, empleadoId, fechaDentro, "[TEST] Turno Dentro De La Cota");
        await PublicarTurnoAsync(solicitudFuera, empleadoId, fechaFuera, "[TEST] Turno Fuera De La Cota");

        // Espera a que AMBAS vistas esten materializadas antes de verificar el recorte: si no
        // esperaramos, la ausencia de "fuera" podria deberse a lag asincrono y no al recorte mismo.
        var dentroMaterializado = await EsperarTurnoMaterializadoAsync(empleadoId, fechaDentro, ct);
        dentroMaterializado.Should().BeTrue(
            $"la vista de {empleadoId} en {fechaDentro} deberia materializarse dentro del timeout");

        var fueraMaterializado = await EsperarTurnoMaterializadoAsync(empleadoId, fechaFuera, ct);
        fueraMaterializado.Should().BeTrue(
            $"la vista de {empleadoId} en {fechaFuera} deberia materializarse dentro del timeout");

        var response = await _client.GetAsync(Ruta(desde, hastaSolicitado, empleadoId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosDiariosSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        // Assert: CA-3 -- recorte SIEMPRE hacia adelante desde `desde`.
        respuesta!.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hastaAplicadaEsperada);
        respuesta.RangoRecortado.Should().BeTrue();

        // El turno dentro del rango recortado aparece; el que queda fuera NO, aunque su vista ya
        // este materializada (verificado arriba) -- prueba que el recorte restringe la consulta.
        respuesta.Turnos.Should().Contain(t => t.UltimaSolicitudId == solicitudDentro);
        respuesta.Turnos.Should().NotContain(t => t.UltimaSolicitudId == solicitudFuera);
    }
}
