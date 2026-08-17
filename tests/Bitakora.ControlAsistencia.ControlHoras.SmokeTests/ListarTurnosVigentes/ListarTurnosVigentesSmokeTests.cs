using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarTurnosVigentes;

// Issue #329: smoke tests de ListarTurnosVigentes, GET control-horas/turnos-vigentes con desde/hasta
// obligatorios y codigoColaborador opcional. Tercera Function GET read-side del BC sobre la MISMA vista
// TurnoVigente que ya materializa #328 -- se siembran datos publicando ProgramacionTurnoDiario-
// Solicitada al bus interno, exactamente el mismo mecanismo de ObtenerTurnoVigenteSmokeTests (#328)
// y la suite del listado anterior (#290).
//
// Estos tests quedan ROJOS hasta que el deploy publique ListarTurnosVigentes en dev: mientras la
// revision anterior siga corriendo, la ruta no existe y el host responde 404 a todo -- los casos
// 400 y 200 fallan por esa razon, no por el contrato. Mismo precedente que el listado anterior
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
//
// Issue #337 (CA-2/CA-3/CA-4): sedeId es un tercer filtro opcional sobre la MISMA Function -- ningun
// endpoint nuevo. Para sembrar bloques con sede se publica ProgramacionTurnoDiarioSolicitada con la
// clave "Sede" DENTRO de cada franja ordinaria (PrivateEvents.Programacion.DetalleFranjaOrdinaria.
// Sede, issue #341) -- nunca a nivel del evento completo: la sede rige por bloque, nunca por dia
// (issue #337, "Contexto"). El mapeo real hasta el bloque de la vista es
// ProgramacionTurnoDiarioSolicitadaEventHandler.MapearFranja -> TurnoDiario.Segmentar (#336) ->
// TurnoVigenteProjection.MapearBloque (#337); este smoke test no lo referencia, solo publica el JSON
// con la forma que ese mapeo espera. El detalle de "el ultimo gana" lo cubre el unit test de la
// proyeccion (projection-test-writer); aqui se verifica el filtro sedeId del endpoint HTTP y, sobre
// esa misma respuesta, que los campos de sede efectivamente VIAJEN al cliente en cada bloque (CA-1
// de punta a punta: la cadena completa, no solo la funcion pura de mapeo).
//
// ObtenerTurnoVigente (#328) NO se toca en este archivo ni en su propia suite: su Function.cs no
// cambio (issue #337, "Endpoints / rutas" -- "sin cambio de firma ni ruta"), aunque su respuesta
// tambien ganaria los campos de sede en los bloques por compartir la misma vista TurnoVigente. El
// alcance de esta tarea son los endpoints modificados, y el unico modificado es este.
public class ListarTurnosVigentesSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = "programacion-turno-diario-solicitada";

    // Descripcion del turno sembrado por PublicarTurnoAsync: es el dato que la vista devuelve como
    // HorarioResumido, asi que se afirma contra esta misma constante (eco del payload publicado).
    private const string DescripcionTurnoSinSede = "[TEST] Turno Vigente Listar 08:00-16:00";

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

    // Issue #337: SedeId/NombreSede son aditivos y opcionales en la vista, asi que tambien lo son en
    // esta forma local -- un bloque sin sede (turno sembrado sin la clave "Sede", o documento
    // proyectado antes de #336/#337) los trae null.
    private sealed record BloqueSmoke(
        TipoBloqueSmoke Tipo,
        DateTime Inicio,
        DateTime Fin,
        string? SedeId = null,
        string? NombreSede = null);

    private sealed record TurnoVigenteRespuestaSmoke(
        string Id,
        string CodigoColaborador,
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

    private static string Ruta(
        DateOnly desde, DateOnly hasta, string? codigoColaborador = null, string? sedeId = null)
    {
        var query = $"desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}";
        if (codigoColaborador is not null)
            query += $"&codigoColaborador={Uri.EscapeDataString(codigoColaborador)}";
        if (sedeId is not null)
            query += $"&sedeId={Uri.EscapeDataString(sedeId)}";

        return $"/api/control-horas/turnos-vigentes?{query}";
    }

    // Envelope UNICO de ProgramacionTurnoDiarioSolicitada para toda la suite: entre escenarios solo
    // cambian las franjas y la descripcion del turno, nunca el colaborador ni la forma del evento
    // (antes del issue #337 esta misma forma vivia triplicada -- MEF-ADR-0018, Rule of Three).
    // FranjasOrdinarias viaja como object[] porque las franjas con y sin la clave "Sede" son tipos
    // anonimos distintos; STJ serializa cada elemento por su tipo en tiempo de ejecucion, asi que
    // ninguna pierde campos al convivir en el mismo arreglo.
    private async Task PublicarProgramacionAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string descripcionTurno, params object[] franjasOrdinarias)
    {
        var evento = new
        {
            SolicitudId = solicitudId,
            Colaborador = new
            {
                CodigoColaborador = codigoColaborador,
                TipoIdentificacion = "CC",
                NumeroIdentificacion = "444555666",
                Nombres = "[TEST] Smoke Listar",
                Apellidos = "[TEST] TurnosVigentes"
            },
            Fecha = fecha.ToString("yyyy-MM-dd"),
            DetalleTurno = new
            {
                Nombre = nombreTurno,
                FranjasOrdinarias = franjasOrdinarias,
                Descripcion = descripcionTurno
            }
        };

        await serviceBus.PublishAsync(TopicEntrada, evento, solicitudId.ToString());
    }

    // Franja ordinaria SIN la clave "Sede" -- forma exacta que ya publicaba #329: sus bloques quedan
    // con SedeId/NombreSede null, el equivalente comportamental de un documento proyectado antes de
    // #336/#337 (CA-4/CA-5).
    private static object FranjaOrdinaria(string horaInicio, string horaFin) =>
        new
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetFin = 0,
            Descansos = Array.Empty<object>(),
            Extras = Array.Empty<object>(),
            Descripcion = (string?)null
        };

    // Issue #337/#341: la clave "Sede" va DENTRO de la franja (DetalleFranjaOrdinaria.Sede), nunca a
    // nivel del evento completo -- la sede rige por bloque, nunca por dia (issue #337, "Contexto").
    private static object FranjaOrdinaria(
        string horaInicio, string horaFin, string sedeId, string nombreSede) =>
        new
        {
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            DiaOffsetFin = 0,
            Descansos = Array.Empty<object>(),
            Extras = Array.Empty<object>(),
            Descripcion = (string?)null,
            Sede = new { Id = sedeId, Nombre = nombreSede }
        };

    private Task PublicarTurnoAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno) =>
        PublicarProgramacionAsync(
            solicitudId, codigoColaborador, fecha, nombreTurno, DescripcionTurnoSinSede,
            FranjaOrdinaria("08:00:00", "16:00:00"));

    // Issue #337: una sola franja que SI trae sede -- escenario de combinacion sedeId + codigoColaborador
    // (CA-3).
    private Task PublicarTurnoConSedeAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string sedeId, string nombreSede) =>
        PublicarProgramacionAsync(
            solicitudId, codigoColaborador, fecha, nombreTurno, $"[TEST] {nombreTurno}",
            FranjaOrdinaria("08:00:00", "16:00:00", sedeId, nombreSede));

    // Issue #337: turno partido en DOS franjas, cada una en su propia sede -- el escenario del
    // "Contexto" del issue (Carlos manana-Suba/tarde-Chapinero). Ambas franjas producen un bloque
    // Ordinaria cada una (sin cruce de horario, sin solape) para que la vista materialice un dia
    // multi-sede real.
    private Task PublicarTurnoMultiSedeAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string sedeIdManana, string nombreSedeManana,
        string sedeIdTarde, string nombreSedeTarde) =>
        PublicarProgramacionAsync(
            solicitudId, codigoColaborador, fecha, nombreTurno, $"[TEST] {nombreTurno}",
            FranjaOrdinaria("08:00:00", "12:00:00", sedeIdManana, nombreSedeManana),
            FranjaOrdinaria("14:00:00", "18:00:00", sedeIdTarde, nombreSedeTarde));

    // Sensa la materializacion asincrona consultando ESTE mismo endpoint sobre un rango de un solo
    // dia (desde == hasta) filtrado por codigoColaborador, que nunca activa el recorte -- no via
    // ObtenerTurnoVigente (#328), para no acoplar el veredicto de este archivo a la salud de otra
    // Function.
    private async Task<bool> EsperarTurnoEnLaListaAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        return await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(fecha, fecha, codigoColaborador), ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body?.Turnos.Any(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha) ?? false;
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

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd, mismo precedente que #290.
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
    public async Task ListarTurnosVigentes_Retorna200ConListaVacia_CuandoNoHayTurnoVigenteParaEseColaborador()
    {
        var ct = TestContext.Current.CancellationToken;

        // Arrange: codigoColaborador nuevo, nunca creado por ningun test -- no puede tener turno vigente.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 5);

        var response = await _client.GetAsync(Ruta(desde, hasta, codigoColaborador), ct);

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
    public async Task ListarTurnosVigentes_Retorna200ConElTurnoDelColaborador_CuandoSeConsultaConCodigoColaborador()
    {
        // CA-2: la consulta del Trabajador -- codigoColaborador presente filtra la lista a ese colaborador.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fechaTurno = new DateOnly(2026, 5, 10);
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 15); // 15 dias, dentro de la cota de 31

        await PublicarTurnoAsync(solicitudId, codigoColaborador, fechaTurno, "[TEST] Turno Vigente Trabajador");

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la vista.
        var ruta = Ruta(desde, hasta, codigoColaborador);
        var respuesta = await Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(ruta, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is { Turnos.Count: > 0 } ? body : null;
        }, Timeout);

        // Assert: CA-4 -- rango dentro de la cota, sin recorte. Filtrado por codigoColaborador (GUID unico
        // de esta ejecucion), asi que la lista contiene exactamente el turno sembrado.
        respuesta.DesdeAplicado.Should().Be(desde);
        respuesta.HastaAplicado.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
        respuesta.Turnos.Should().ContainSingle();

        // Assert: cada elemento lleva la forma completa de la vista (Id y Bloques incluidos --
        // decision de entrevista del issue #329, "Notas tecnicas": una sola proyeccion sirve grilla
        // y calendario).
        var turno = respuesta.Turnos[0];
        turno.Id.Should().Be($"{codigoColaborador}:{fechaTurno:yyyy-MM-dd}");
        turno.CodigoColaborador.Should().Be(codigoColaborador);
        turno.NombreCompleto.Should().Be("[TEST] Smoke Listar [TEST] TurnosVigentes");
        turno.Fecha.Should().Be(fechaTurno);
        turno.NombreTurno.Should().Be("[TEST] Turno Vigente Trabajador");
        turno.HorarioResumido.Should().Be(DescripcionTurnoSinSede);

        // Issue #337: el turno sembrado no trae sede, asi que el bloque llega con SedeId/NombreSede
        // null (valores por defecto de BloqueSmoke) -- regresion de #329 intacta.
        var bloqueEsperado = new BloqueSmoke(
            TipoBloqueSmoke.Ordinaria,
            fechaTurno.ToDateTime(new TimeOnly(8, 0)),
            fechaTurno.ToDateTime(new TimeOnly(16, 0)));
        turno.Bloques.Should().BeEquivalentTo([bloqueEsperado]);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_IncluyeATodosLosColaboradores_CuandoNoSeFiltraPorCodigoColaborador()
    {
        // CA-1: el panorama del Programador -- SIN codigoColaborador, la lista trae los turnos vigentes de
        // TODOS los colaboradores en el rango. Se publican 2 colaboradores distintos (no 1) para distinguir
        // "trae el panorama completo" de "trae un solo turno".
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var fecha = new DateOnly(2026, 5, 20);
        var solicitudA = Guid.CreateVersion7();
        var solicitudB = Guid.CreateVersion7();
        var codigoColaboradorA = Guid.CreateVersion7().ToString();
        var codigoColaboradorB = Guid.CreateVersion7().ToString();

        await PublicarTurnoAsync(solicitudA, codigoColaboradorA, fecha, "[TEST] Turno Panorama A");
        await PublicarTurnoAsync(solicitudB, codigoColaboradorB, fecha, "[TEST] Turno Panorama B");

        // Sin codigoColaborador: la lista puede incluir turnos de otras ejecuciones de esta suite (sin
        // cleanup, GUIDs unicos por corrida) -- se espera a que AMBOS aparezcan antes de assertar,
        // filtrando siempre por CodigoColaborador, nunca por posicion/indice.
        var ambosMaterializados = await Polling.WaitUntilTrueAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(fecha, fecha), ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return (body?.Turnos.Any(t => t.CodigoColaborador == codigoColaboradorA) ?? false)
                && (body?.Turnos.Any(t => t.CodigoColaborador == codigoColaboradorB) ?? false);
        }, Timeout);

        ambosMaterializados.Should().BeTrue(
            "ambos colaboradores deberian aparecer en el panorama dentro del timeout");

        var response = await _client.GetAsync(Ruta(fecha, fecha), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        // Assert: CA-1 -- el panorama trae ambos colaboradores, cada uno con su propio turno.
        var turnoA = respuesta!.Turnos.Single(t => t.CodigoColaborador == codigoColaboradorA);
        var turnoB = respuesta.Turnos.Single(t => t.CodigoColaborador == codigoColaboradorB);
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

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 6, 1);
        var hastaSolicitado = new DateOnly(2026, 9, 30); // ~121 dias, muy por encima de la cota
        var hastaAplicadaEsperada = desde.AddDays(30); // CA-3: cota de 31 dias inclusive

        var solicitudDentro = Guid.CreateVersion7();
        var solicitudFuera = Guid.CreateVersion7();
        var fechaDentro = desde; // dentro del rango recortado
        var fechaFuera = hastaAplicadaEsperada.AddDays(5); // dentro de lo pedido, fuera del recorte

        await PublicarTurnoAsync(solicitudDentro, codigoColaborador, fechaDentro, "[TEST] Turno Vigente Dentro De La Cota");
        await PublicarTurnoAsync(solicitudFuera, codigoColaborador, fechaFuera, "[TEST] Turno Vigente Fuera De La Cota");

        // Espera a que AMBAS vistas esten materializadas antes de verificar el recorte: si no
        // esperaramos, la ausencia de "fuera" podria deberse a lag asincrono y no al recorte mismo.
        var dentroMaterializado = await EsperarTurnoEnLaListaAsync(codigoColaborador, fechaDentro, ct);
        dentroMaterializado.Should().BeTrue(
            $"la vista de {codigoColaborador} en {fechaDentro} deberia materializarse dentro del timeout");

        var fueraMaterializado = await EsperarTurnoEnLaListaAsync(codigoColaborador, fechaFuera, ct);
        fueraMaterializado.Should().BeTrue(
            $"la vista de {codigoColaborador} en {fechaFuera} deberia materializarse dentro del timeout");

        var response = await _client.GetAsync(Ruta(desde, hastaSolicitado, codigoColaborador), ct);
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_IncluyeElDiaBajoCualquieraDeSusSedes_CuandoElTurnoEsMultiSede()
    {
        // CA-2 (issue #337): escenario del "Contexto" -- Carlos con manana-Suba/tarde-Chapinero
        // aparece en el panorama de AMBOS jefes de sede, porque el filtro es "al menos un bloque
        // rige en esa sede", no "el dia entero pertenece a una sola sede".
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 5);
        var sedeIdManana = Guid.CreateVersion7().ToString();
        var sedeIdTarde = Guid.CreateVersion7().ToString();
        var sedeIdSinTurnosDeEsteColaborador = Guid.CreateVersion7().ToString();
        const string nombreSedeManana = "[TEST] Sede Suba";
        const string nombreSedeTarde = "[TEST] Sede Chapinero";

        await PublicarTurnoMultiSedeAsync(
            solicitudId, codigoColaborador, fecha, "[TEST] Turno Multi Sede",
            sedeIdManana, nombreSedeManana, sedeIdTarde, nombreSedeTarde);

        var materializado = await EsperarTurnoEnLaListaAsync(codigoColaborador, fecha, ct);
        materializado.Should().BeTrue(
            $"la vista de {codigoColaborador} en {fecha} deberia materializarse dentro del timeout");

        // Assert: aparece filtrando por la sede de la franja de la manana...
        var responseManana = await _client.GetAsync(Ruta(fecha, fecha, codigoColaborador, sedeIdManana), ct);
        responseManana.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaManana = await responseManana.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaManana!.Turnos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // Assert (CA-1 de punta a punta): la sede no solo filtra, tambien VIAJA al cliente en cada
        // bloque -- cada uno con la sede de SU PROPIA franja, no la del dia. Filtrar por la sede de
        // la manana devuelve el dia COMPLETO (incluido el bloque de la tarde en otra sede): es la
        // evidencia de que el predicado es "al menos un bloque rige en esa sede", no un recorte de
        // los bloques devueltos.
        var turnoMultiSede = respuestaManana.Turnos.Single(
            t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);
        turnoMultiSede.Bloques.Should().BeEquivalentTo(new[]
        {
            new BloqueSmoke(
                TipoBloqueSmoke.Ordinaria,
                fecha.ToDateTime(new TimeOnly(8, 0)), fecha.ToDateTime(new TimeOnly(12, 0)),
                sedeIdManana, nombreSedeManana),
            new BloqueSmoke(
                TipoBloqueSmoke.Ordinaria,
                fecha.ToDateTime(new TimeOnly(14, 0)), fecha.ToDateTime(new TimeOnly(18, 0)),
                sedeIdTarde, nombreSedeTarde)
        });

        // ...y TAMBIEN filtrando por la sede de la franja de la tarde -- mismo dia, dos sedes.
        var responseTarde = await _client.GetAsync(Ruta(fecha, fecha, codigoColaborador, sedeIdTarde), ct);
        responseTarde.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaTarde = await responseTarde.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaTarde!.Turnos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // Assert: NO aparece bajo una sede que ningun bloque de este turno tiene.
        var responseOtraSede = await _client.GetAsync(
            Ruta(fecha, fecha, codigoColaborador, sedeIdSinTurnosDeEsteColaborador), ct);
        responseOtraSede.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaOtraSede = await responseOtraSede.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaOtraSede!.Turnos.Should().NotContain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_ExcluyeElDiaSinSede_CuandoSeFiltraPorSedeId()
    {
        // CA-4 (issue #337): un dia cuyos bloques no tienen sede (franja sin sede asignada, o
        // documento proyectado antes de #336/#337 -- CA-5) no aparece bajo NINGUN sedeId, pero SI
        // en la consulta sin filtro. Regresion de #329 verificada explicitamente en el mismo test.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 6);
        var sedeIdCualquiera = Guid.CreateVersion7().ToString();

        // PublicarTurnoAsync (helper de #329) nunca incluye la clave "Sede" en la franja --
        // equivalente comportamental a un bloque con SedeId/NombreSede null (CA-4/CA-5).
        await PublicarTurnoAsync(solicitudId, codigoColaborador, fecha, "[TEST] Turno Sin Sede");

        var materializado = await EsperarTurnoEnLaListaAsync(codigoColaborador, fecha, ct);
        materializado.Should().BeTrue(
            $"la vista de {codigoColaborador} en {fecha} deberia materializarse dentro del timeout");

        // Assert: filtrando por CUALQUIER sedeId, el dia sin sede queda fuera.
        var responseFiltrada = await _client.GetAsync(
            Ruta(fecha, fecha, codigoColaborador, sedeIdCualquiera), ct);
        responseFiltrada.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaFiltrada = await responseFiltrada.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaFiltrada!.Turnos.Should().NotContain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // Assert: sin filtro de sede, el dia SI aparece -- regresion de #329 intacta.
        var responseSinFiltro = await _client.GetAsync(Ruta(fecha, fecha, codigoColaborador), ct);
        responseSinFiltro.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaSinFiltro = await responseSinFiltro.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaSinFiltro!.Turnos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // Assert (CA-5): el dia se consulta SIN ERROR y sus bloques llegan con ambos campos de sede
        // en null -- el mismo contrato que devuelve un documento proyectado antes de #336/#337.
        var turnoSinSede = respuestaSinFiltro.Turnos.Single(
            t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);
        turnoSinSede.Bloques.Should().OnlyContain(b => b.SedeId == null && b.NombreSede == null);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_CombinaSedeIdConCodigoColaborador_CuandoAmbosFiltrosSeAplican()
    {
        // CA-3 (issue #337): sedeId es combinable con codigoColaborador -- misma sede, dos colaboradores
        // distintos, el filtro compuesto (AND) devuelve solo al colaborador pedido.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var fecha = new DateOnly(2026, 6, 7);
        var sedeId = Guid.CreateVersion7().ToString();
        var solicitudA = Guid.CreateVersion7();
        var solicitudB = Guid.CreateVersion7();
        var codigoColaboradorA = Guid.CreateVersion7().ToString();
        var codigoColaboradorB = Guid.CreateVersion7().ToString();

        await PublicarTurnoConSedeAsync(
            solicitudA, codigoColaboradorA, fecha, "[TEST] Turno Combinado A", sedeId, "[TEST] Sede Combinada");
        await PublicarTurnoConSedeAsync(
            solicitudB, codigoColaboradorB, fecha, "[TEST] Turno Combinado B", sedeId, "[TEST] Sede Combinada");

        var aMaterializado = await EsperarTurnoEnLaListaAsync(codigoColaboradorA, fecha, ct);
        aMaterializado.Should().BeTrue(
            $"la vista de {codigoColaboradorA} en {fecha} deberia materializarse dentro del timeout");

        var bMaterializado = await EsperarTurnoEnLaListaAsync(codigoColaboradorB, fecha, ct);
        bMaterializado.Should().BeTrue(
            $"la vista de {codigoColaboradorB} en {fecha} deberia materializarse dentro del timeout");

        // Act: combinar sedeId (compartido por A y B) + codigoColaborador=A.
        var response = await _client.GetAsync(Ruta(fecha, fecha, codigoColaboradorA, sedeId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        // Assert: solo el turno de A -- el filtro compuesto excluye a B aunque comparta la sede.
        respuesta!.Turnos.Should().ContainSingle(t => t.CodigoColaborador == codigoColaboradorA && t.Fecha == fecha);
        respuesta.Turnos.Should().NotContain(t => t.CodigoColaborador == codigoColaboradorB);
    }
}
