using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarTurnosVigentes;

// Issue #329: smoke tests de ListarTurnosVigentes, GET control-horas/turnos-vigentes con desde/hasta
// obligatorios y empleadoId opcional. Tercera Function GET read-side del BC sobre la MISMA vista
// TurnoVigente que ya materializa #328 -- se siembran datos publicando ProgramacionTurnoDiario-
// Solicitada al bus interno, exactamente el mismo mecanismo de ObtenerTurnoVigenteSmokeTests (#328)
// y ListarTurnosDiariosSmokeTests (#290).
//
// Estos tests quedan ROJOS hasta que el deploy publique ListarTurnosVigentes en dev: mientras la
// revision anterior siga corriendo, la ruta no existe y el host responde 404 a todo -- los casos
// 400 y 200 fallan por esa razon, no por el contrato. Mismo precedente que ListarTurnosDiarios
// (#290) y ObtenerTurnoVigente (#328). El CI de PR no los ejecuta (solo corre *.Tests); su
// veredicto real se lee despues del deploy.
//
// La proyeccion tiene lifecycle Async (MEF-ADR-0034): el worker la materializa DESPUES de que
// ControlHoras persiste turno_diario_asignado. Los casos que dependen de datos sembrados envuelven
// la consulta en Polling.WaitUntilAsync/WaitUntilTrueAsync (timeout estandar 30s) -- si el timeout
// se agota es un fallo real (worker no desplegado o proyeccion sin registrar), nunca un skip.
//
// Formas locales DESACOPLADAS del read model de produccion (Bitakora.ControlAsistencia.ReadModels.
// ControlHoras.TurnoVigente/Bloque/TipoBloque) y del envelope de produccion (ControlHoras.
// ListarTurnosVigentes.ListaTurnosVigentes): el smoke test no referencia ReadModels (isla, MEF-ADR-
// 0034 seccion 5) ni el Function App. TipoBloqueSmoke replica el orden de valores del enum de
// produccion porque STJ lo serializa como el entero subyacente (mismo razonamiento documentado en
// ObtenerTurnoVigenteSmokeTests). El limite de 31 dias se afirma como literal (desde + 30 dias),
// nunca leyendo RangoConsulta.CotaDias.
//
// Sin PostgresFixture: la verificacion de persistencia del evento turno_diario_asignado ya la cubre
// AsignarTurnoViaSbSmokeTests (issue #322); aqui solo interesa que la vista materializada llegue al
// endpoint HTTP -- mismo alcance que ObtenerTurnoVigenteSmokeTests.
public class ListarTurnosVigentesSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
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

    private enum TipoBloqueSmoke
    {
        Ordinaria,
        Descanso,
        Extra
    }

    private sealed record BloqueSmoke(TipoBloqueSmoke Tipo, DateTime Inicio, DateTime Fin);

    private sealed record TurnoVigenteRespuestaSmoke(
        string Id,
        string EmpleadoId,
        string NombreCompleto,
        DateOnly Fecha,
        string NombreTurno,
        string HorarioResumido,
        IReadOnlyList<BloqueSmoke> Bloques);

    private sealed record ListaTurnosVigentesSmoke(
        DateOnly DesdeAplicado,
        DateOnly HastaAplicado,
        bool RangoRecortado,
        IReadOnlyList<TurnoVigenteRespuestaSmoke> Turnos);

    private static string Ruta(DateOnly desde, DateOnly hasta, string? empleadoId = null)
    {
        var query = $"desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        if (empleadoId is not null)
            query += $"&empleadoId={Uri.EscapeDataString(empleadoId)}";

        return $"/api/control-horas/turnos-vigentes?{query}";
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
                NumeroIdentificacion = "444555666",
                Nombres = "[TEST] Smoke Listar",
                Apellidos = "[TEST] TurnosVigentes"
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
                Descripcion = "[TEST] Turno Vigente Listar 08:00-16:00"
            }
        };

        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());
    }

    // Sensa la materializacion asincrona consultando ESTE mismo endpoint sobre un rango de un solo
    // dia (desde == hasta) filtrado por empleadoId, que nunca activa el recorte -- no via
    // ObtenerTurnoVigente (#328), para no acoplar el veredicto de este archivo a la salud de otra
    // Function.
    private async Task<bool> EsperarTurnoEnLaListaAsync(
        string empleadoId, DateOnly fecha, CancellationToken ct)
    {
        return await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(fecha, fecha, empleadoId), ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body?.Turnos.Any(t => t.EmpleadoId == empleadoId && t.Fecha == fecha) ?? false;
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
    public async Task ListarTurnosVigentes_Retorna400_CuandoFaltaElParametroDesde()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            "/api/control-horas/turnos-vigentes?hasta=2026-05-10", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoFaltaElParametroHasta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            "/api/control-horas/turnos-vigentes?desde=2026-05-01", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoDesdeTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd, mismo precedente que ListarTurnosDiarios (#290).
        var response = await _client.GetAsync(
            "/api/control-horas/turnos-vigentes?desde=01-05-2026&hasta=2026-05-10", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoHastaEsAnteriorADesde()
    {
        var ct = TestContext.Current.CancellationToken;

        // Rango invertido: decision documentada en FunctionEndpoint.cs (400, no lista vacia).
        var desde = new DateOnly(2026, 5, 10);
        var hasta = new DateOnly(2026, 5, 5);

        var response = await _client.GetAsync(Ruta(desde, hasta), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna200ConListaVacia_CuandoNoHayTurnoVigenteParaEseEmpleado()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: empleadoId nuevo, nunca creado por ningun test -- no puede tener turno vigente.
        var empleadoId = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 5);

        var response = await _client.GetAsync(Ruta(desde, hasta, empleadoId), ct);

        // Assert: CA-4 -- 200 con Turnos: [], nunca 404. Rango dentro de la cota, sin recorte.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.Turnos.Should().BeEmpty();
        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna200ConElTurnoDelEmpleado_CuandoSeConsultaConEmpleadoId()
    {
        // CA-2: la consulta del Trabajador -- empleadoId presente filtra la lista a ese empleado.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var empleadoId = Guid.CreateVersion7().ToString();
        var fechaTurno = new DateOnly(2026, 5, 10);
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 15); // 15 dias, dentro de la cota de 31

        await PublicarTurnoAsync(solicitudId, empleadoId, fechaTurno, "[TEST] Turno Vigente Trabajador");

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la vista.
        var ruta = Ruta(desde, hasta, empleadoId);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is { Turnos.Count: > 0 } ? body : null;
        }, Timeout);

        // Assert: CA-4 -- rango dentro de la cota, sin recorte. Filtrado por empleadoId (GUID unico
        // de esta ejecucion), asi que la lista contiene exactamente el turno sembrado.
        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
        respuesta.Turnos.Should().ContainSingle();

        // Assert: cada elemento lleva la forma completa de la vista (Id y Bloques incluidos --
        // decision de entrevista del issue #329, "Notas tecnicas": una sola proyeccion sirve grilla
        // y calendario).
        var turno = respuesta.Turnos[0];
        turno.Id.Should().Be($"{empleadoId}:{fechaTurno:yyyy-MM-dd}");
        turno.EmpleadoId.Should().Be(empleadoId);
        turno.NombreCompleto.Should().Be("[TEST] Smoke Listar [TEST] TurnosVigentes");
        turno.Fecha.Should().Be(fechaTurno);
        turno.NombreTurno.Should().Be("[TEST] Turno Vigente Trabajador");
        turno.HorarioResumido.Should().Be("[TEST] Turno Vigente Listar 08:00-16:00");

        var bloqueEsperado = new BloqueSmoke(
            TipoBloqueSmoke.Ordinaria,
            fechaTurno.ToDateTime(new TimeOnly(8, 0)),
            fechaTurno.ToDateTime(new TimeOnly(16, 0)));
        turno.Bloques.Should().BeEquivalentTo([bloqueEsperado]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_IncluyeATodosLosEmpleados_CuandoNoSeFiltraPorEmpleadoId()
    {
        // CA-1: el panorama del Programador -- SIN empleadoId, la lista trae los turnos vigentes de
        // TODOS los empleados en el rango. Se publican 2 empleados distintos (no 1) para distinguir
        // "trae el panorama completo" de "trae un solo turno".
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var fecha = new DateOnly(2026, 5, 20);
        var solicitudA = Guid.CreateVersion7();
        var solicitudB = Guid.CreateVersion7();
        var empleadoIdA = Guid.CreateVersion7().ToString();
        var empleadoIdB = Guid.CreateVersion7().ToString();

        await PublicarTurnoAsync(solicitudA, empleadoIdA, fecha, "[TEST] Turno Panorama A");
        await PublicarTurnoAsync(solicitudB, empleadoIdB, fecha, "[TEST] Turno Panorama B");

        // Sin empleadoId: la lista puede incluir turnos de otras ejecuciones de esta suite (sin
        // cleanup, GUIDs unicos por corrida) -- se espera a que AMBOS aparezcan antes de assertar,
        // filtrando siempre por EmpleadoId, nunca por posicion/indice.
        var ambosMaterializados = await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(fecha, fecha), ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return (body?.Turnos.Any(t => t.EmpleadoId == empleadoIdA) ?? false)
                && (body?.Turnos.Any(t => t.EmpleadoId == empleadoIdB) ?? false);
        }, Timeout);

        ambosMaterializados.Should().BeTrue(
            "ambos empleados deberian aparecer en el panorama dentro del timeout");

        var response = await _client.GetAsync(Ruta(fecha, fecha), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        // Assert: CA-1 -- el panorama trae ambos empleados, cada uno con su propio turno.
        var turnoA = respuesta!.Turnos.Single(t => t.EmpleadoId == empleadoIdA);
        var turnoB = respuesta.Turnos.Single(t => t.EmpleadoId == empleadoIdB);
        turnoA.NombreTurno.Should().Be("[TEST] Turno Panorama A");
        turnoB.NombreTurno.Should().Be("[TEST] Turno Panorama B");
        respuesta.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_RecortaHaciaAdelanteYExcluyeLoQueQuedaFuera_CuandoElRangoExcedeLaCotaDe31Dias()
    {
        // CA-3: se verifica que el recorte SI restringe la consulta, no solo que se declara en el
        // envelope.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var empleadoId = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 6, 1);
        var hastaSolicitado = new DateOnly(2026, 9, 30); // ~121 dias, muy por encima de la cota
        var hastaAplicadaEsperada = desde.AddDays(30); // CA-3: cota de 31 dias inclusive

        var solicitudDentro = Guid.CreateVersion7();
        var solicitudFuera = Guid.CreateVersion7();
        var fechaDentro = desde; // dentro del rango recortado
        var fechaFuera = hastaAplicadaEsperada.AddDays(5); // dentro de lo pedido, fuera del recorte

        await PublicarTurnoAsync(solicitudDentro, empleadoId, fechaDentro, "[TEST] Turno Vigente Dentro De La Cota");
        await PublicarTurnoAsync(solicitudFuera, empleadoId, fechaFuera, "[TEST] Turno Vigente Fuera De La Cota");

        // Espera a que AMBAS vistas esten materializadas antes de verificar el recorte: si no
        // esperaramos, la ausencia de "fuera" podria deberse a lag asincrono y no al recorte mismo.
        var dentroMaterializado = await EsperarTurnoEnLaListaAsync(empleadoId, fechaDentro, ct);
        dentroMaterializado.Should().BeTrue(
            $"la vista de {empleadoId} en {fechaDentro} deberia materializarse dentro del timeout");

        var fueraMaterializado = await EsperarTurnoEnLaListaAsync(empleadoId, fechaFuera, ct);
        fueraMaterializado.Should().BeTrue(
            $"la vista de {empleadoId} en {fechaFuera} deberia materializarse dentro del timeout");

        var response = await _client.GetAsync(Ruta(desde, hastaSolicitado, empleadoId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        // Assert: CA-3 -- recorte SIEMPRE hacia adelante desde `desde`.
        respuesta!.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hastaAplicadaEsperada);
        respuesta.RangoRecortado.Should().BeTrue();

        // El turno dentro del rango recortado aparece; el que queda fuera NO, aunque su vista ya
        // este materializada (verificado arriba) -- prueba que el recorte restringe la consulta.
        respuesta.Turnos.Should().Contain(t => t.Fecha == fechaDentro);
        respuesta.Turnos.Should().NotContain(t => t.Fecha == fechaFuera);
    }
}
