// Arrange via el bus interno (ProgramacionTurnoEnFechasSolicitada), nunca sembrando el event store por fuera.
// Un 404 (no 405) al verbo QUERY tras un deploy exitoso apunta al borde de App Service filtrando un verbo
// no estandar, no al endpoint (MEF-ADR-0042 seccion 6, sin verificar).
// La proyeccion es Async: la consulta va en Polling y agotar el timeout es un fallo real, no un skip.
// Las formas locales no referencian ReadModels ni el Function App (MEF-ADR-0034 seccion 5); TipoBloqueSmoke
// replica el ORDEN del enum de produccion porque STJ lo serializa como entero.
// Sin PostgresFixture: la persistencia de turno_diario_asignado la cubre AsignarTurnoViaSbSmokeTests.
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;
using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.ListarTurnosVigentes;

public class ListarTurnosVigentesSmokeTests(ApiFixture api, ServiceBusFixture serviceBus)
{
    private readonly HttpClient _client = api.Client;

    private const string TopicEntrada = ProgramacionEnLote.Topic;
    private const string RutaListado = "/api/control-horas/turnos-vigentes";
    private static readonly HttpMethod MetodoQuery = new("QUERY");

    // Eco del payload publicado: la vista lo devuelve como HorarioResumido.
    private const string DescripcionTurnoSinSede = "[TEST] Turno Vigente Listar 08:00-16:00";

    // Eco del colaborador sembrado: la vista devuelve NombreCompleto tal cual viaja en el evento.
    private const string NombreCompletoSembrado = "[TEST] Smoke Listar TurnosVigentes";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La respuesta viaja en camelCase y las formas locales son PascalCase.
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

    // SedeId/NombreSede son opcionales en la vista: llegan null en un bloque sin sede.
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
        DateOnly Desde,
        DateOnly Hasta,
        bool RangoRecortado,
        IReadOnlyList<TurnoVigenteRespuestaSmoke> Elementos,
        string? SiguienteCursor);

    // camelCase explicito: sobre un tipo anonimo no aplica ninguna naming policy, asi que estos
    // nombres son literalmente el contrato de FiltroListarTurnosVigentes.
    private static object Filtro(
        DateOnly desde, DateOnly hasta, string? codigoColaborador = null, string? sedeId = null) =>
        new
        {
            desdeFecha = desde,
            hastaFecha = hasta,
            codigosColaborador = codigoColaborador is null ? null : new[] { codigoColaborador },
            sedeId
        };

    private static object FiltroConLista(DateOnly desde, DateOnly hasta, params string[] codigosColaborador) =>
        new { desdeFecha = desde, hastaFecha = hasta, codigosColaborador };

    private async Task<HttpResponseMessage> ConsultarAsync(object filtro, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = JsonContent.Create(filtro)
        };
        return await _client.SendAsync(request, ct);
    }

    // Body crudo, para los casos que el DTO no puede expresar: JSON malformado, body vacio o un
    // Content-Type distinto de application/json.
    private async Task<HttpResponseMessage> ConsultarCrudoAsync(
        string body, string contentType, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(MetodoQuery, RutaListado)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType)
        };
        return await _client.SendAsync(request, ct);
    }

    private async Task<ListaTurnosVigentesSmoke> ConsultarOkAsync(object filtro, CancellationToken ct)
    {
        using var response = await ConsultarAsync(filtro, ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        body.Should().NotBeNull();
        return body!;
    }

    private Task<ListaTurnosVigentesSmoke> ConsultarHastaQueAsync(
        object filtro, Func<ListaTurnosVigentesSmoke, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            using var response = await ConsultarAsync(filtro, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body is not null && condicion(body) ? body : null;
        }, Timeout);

    // FranjasOrdinarias es object[] porque las franjas con y sin "Sede" son tipos anonimos distintos;
    // STJ serializa cada elemento por su tipo real, asi que ninguna pierde campos.
    private async Task PublicarProgramacionAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string descripcionTurno, params object[] franjasOrdinarias)
    {
        var evento = new
        {
            SolicitudId = solicitudId,
            Colaborador = new ResumenColaborador(
                "CC-444555666", codigoColaborador, NombreCompletoSembrado),
            Fechas = new[] { fecha.ToString("yyyy-MM-dd") },
            Jornada = ProgramacionEnLote.Jornada,
            DetalleTurno = new
            {
                Nombre = nombreTurno,
                FranjasOrdinarias = franjasOrdinarias,
                Descripcion = descripcionTurno
            }
        };

        await serviceBus.PublishAsync(codigoColaborador, TopicEntrada, evento, solicitudId.ToString());
    }

    // Franja ordinaria SIN la clave "Sede": sus bloques quedan con SedeId/NombreSede null.
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

    // "Sede" va dentro de la franja, no en el evento: la sede rige por bloque, no por dia.
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

    private Task PublicarTurnoConSedeAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string sedeId, string nombreSede) =>
        PublicarProgramacionAsync(
            solicitudId, codigoColaborador, fecha, nombreTurno, $"[TEST] {nombreTurno}",
            FranjaOrdinaria("08:00:00", "16:00:00", sedeId, nombreSede));

    private Task PublicarTurnoMultiSedeAsync(
        Guid solicitudId, string codigoColaborador, DateOnly fecha, string nombreTurno,
        string sedeIdManana, string nombreSedeManana,
        string sedeIdTarde, string nombreSedeTarde) =>
        PublicarProgramacionAsync(
            solicitudId, codigoColaborador, fecha, nombreTurno, $"[TEST] {nombreTurno}",
            FranjaOrdinaria("08:00:00", "12:00:00", sedeIdManana, nombreSedeManana),
            FranjaOrdinaria("14:00:00", "18:00:00", sedeIdTarde, nombreSedeTarde));

    // Sensa la materializacion con este mismo endpoint (un dia, filtrado por colaborador, sin recorte) y
    // no via ObtenerTurnoVigente, para no acoplar el veredicto a la salud de otra Function.
    private async Task<bool> EsperarTurnoEnLaListaAsync(
        string codigoColaborador, DateOnly fecha, CancellationToken ct)
    {
        return await Polling.WaitUntilTrueAsync(async () =>
        {
            using var response = await ConsultarAsync(Filtro(fecha, fecha, codigoColaborador), ct);
            if (response.StatusCode != HttpStatusCode.OK)
                return false;

            var body = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
                JsonOptions, cancellationToken: ct);
            return body?.Elementos.Any(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha) ?? false;
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
    public async Task ListarTurnosVigentes_Retorna415_CuandoContentTypeNoEsJson()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{}", "text/plain", ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyNoEsJsonValido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync("{ esto no es json valido", "application/json", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoElBodyEstaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await ConsultarCrudoAsync(string.Empty, "application/json", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna400_CuandoDesdeFechaTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        // Formato DD-MM-YYYY en vez de yyyy-MM-dd: DateOnly? no lo parsea -> JsonException -> 400,
        // distinto del 422 de "campo ausente".
        var response = await ConsultarCrudoAsync(
            """{"desdeFecha":"01-05-2026","hastaFecha":"2026-05-10"}""", "application/json", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna422_CuandoFaltaDesdeFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { hastaFecha = new DateOnly(2026, 5, 10) };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna422_CuandoFaltaHastaFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        var filtro = new { desdeFecha = new DateOnly(2026, 5, 1) };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna422_CuandoHastaFechaEsAnteriorADesdeFecha()
    {
        var ct = TestContext.Current.CancellationToken;

        // Rango invertido: decision documentada en FunctionEndpoint.cs (422, no lista vacia).
        var desde = new DateOnly(2026, 5, 10);
        var hasta = new DateOnly(2026, 5, 5);

        var response = await ConsultarAsync(Filtro(desde, hasta), ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna422_CuandoElCursorEsUnCodigoCrudo()
    {
        var ct = TestContext.Current.CancellationToken;

        // La forma vieja del cursor (el codigo del colaborador en claro) ya no se acepta.
        var filtro = new { desdeFecha = new DateOnly(2026, 5, 1), hastaFecha = new DateOnly(2026, 5, 5), cursor = "EMP-001" };
        var response = await ConsultarAsync(filtro, ct);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna200ConListaVacia_CuandoNoHayTurnoVigenteParaEseColaborador()
    {
        var ct = TestContext.Current.CancellationToken;

        // Codigo nunca creado por ningun test: no puede tener turno vigente.
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 5);

        var response = await ConsultarAsync(Filtro(desde, hasta, codigoColaborador), ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta.Should().NotBeNull();
        respuesta!.Elementos.Should().BeEmpty();
        respuesta.Desde.Should().Be(desde);
        respuesta.Hasta.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_Retorna200ConElTurnoDelColaborador_CuandoSeConsultaConCodigoColaborador()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fechaTurno = new DateOnly(2026, 5, 10);
        var desde = new DateOnly(2026, 5, 1);
        var hasta = new DateOnly(2026, 5, 15);

        await PublicarTurnoAsync(solicitudId, codigoColaborador, fechaTurno, "[TEST] Turno Vigente Trabajador");

        var filtro = Filtro(desde, hasta, codigoColaborador);
        var respuesta = await ConsultarHastaQueAsync(filtro, body => body.Elementos.Count > 0, ct);

        // Filtrado por un GUID unico de esta corrida: la lista contiene exactamente el turno sembrado.
        respuesta.Desde.Should().Be(desde);
        respuesta.Hasta.Should().Be(hasta);
        respuesta.RangoRecortado.Should().BeFalse();
        respuesta.Elementos.Should().ContainSingle();

        // Forma completa, Id y Bloques incluidos: una sola proyeccion sirve la grilla y el calendario.
        var turno = respuesta.Elementos[0];
        turno.Id.Should().Be($"cd:{codigoColaborador}:{fechaTurno:yyyyMMdd}");
        turno.CodigoColaborador.Should().Be(codigoColaborador);
        turno.NombreCompleto.Should().Be(NombreCompletoSembrado);
        turno.Fecha.Should().Be(fechaTurno);
        turno.NombreTurno.Should().Be("[TEST] Turno Vigente Trabajador");
        turno.HorarioResumido.Should().Be(DescripcionTurnoSinSede);

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
        // Dos colaboradores (no uno) para distinguir el panorama completo de un solo turno.
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

        // Sin codigoColaborador la lista trae turnos de otras corridas: se espera a ambos y se filtra por
        // CodigoColaborador, nunca por posicion.
        var filtro = new { desdeFecha = fecha, hastaFecha = fecha };
        var respuesta = await ConsultarHastaQueAsync(filtro, body =>
            body.Elementos.Any(t => t.CodigoColaborador == codigoColaboradorA)
            && body.Elementos.Any(t => t.CodigoColaborador == codigoColaboradorB), ct);

        var turnoA = respuesta.Elementos.Single(t => t.CodigoColaborador == codigoColaboradorA);
        var turnoB = respuesta.Elementos.Single(t => t.CodigoColaborador == codigoColaboradorB);
        turnoA.NombreTurno.Should().Be("[TEST] Turno Panorama A");
        turnoB.NombreTurno.Should().Be("[TEST] Turno Panorama B");
        respuesta.RangoRecortado.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_IncluyeElDiaBajoCualquieraDeSusSedes_CuandoElTurnoEsMultiSede()
    {
        // El filtro de sede es "al menos un bloque rige en esa sede", no "el dia entero es de una sede".
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

        var responseManana = await ConsultarAsync(Filtro(fecha, fecha, codigoColaborador, sedeIdManana), ct);
        responseManana.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaManana = await responseManana.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaManana!.Elementos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // La sede viaja en cada bloque, la de su propia franja; filtrar por la sede de la manana devuelve el
        // dia completo: el predicado no recorta bloques.
        var turnoMultiSede = respuestaManana.Elementos.Single(
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

        var responseTarde = await ConsultarAsync(Filtro(fecha, fecha, codigoColaborador, sedeIdTarde), ct);
        responseTarde.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaTarde = await responseTarde.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaTarde!.Elementos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        var responseOtraSede = await ConsultarAsync(
            Filtro(fecha, fecha, codigoColaborador, sedeIdSinTurnosDeEsteColaborador), ct);
        responseOtraSede.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaOtraSede = await responseOtraSede.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaOtraSede!.Elementos.Should().NotContain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_ExcluyeElDiaSinSede_CuandoSeFiltraPorSedeId()
    {
        // Un dia sin sede en sus bloques no aparece bajo ningun sedeId, pero si en la consulta sin filtro.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var solicitudId = Guid.CreateVersion7();
        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 6);
        var sedeIdCualquiera = Guid.CreateVersion7().ToString();

        // PublicarTurnoAsync nunca incluye la clave "Sede": equivale a bloques con sede null.
        await PublicarTurnoAsync(solicitudId, codigoColaborador, fecha, "[TEST] Turno Sin Sede");

        var materializado = await EsperarTurnoEnLaListaAsync(codigoColaborador, fecha, ct);
        materializado.Should().BeTrue(
            $"la vista de {codigoColaborador} en {fecha} deberia materializarse dentro del timeout");

        var responseFiltrada = await ConsultarAsync(
            Filtro(fecha, fecha, codigoColaborador, sedeIdCualquiera), ct);
        responseFiltrada.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaFiltrada = await responseFiltrada.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaFiltrada!.Elementos.Should().NotContain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        var responseSinFiltro = await ConsultarAsync(Filtro(fecha, fecha, codigoColaborador), ct);
        responseSinFiltro.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuestaSinFiltro = await responseSinFiltro.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuestaSinFiltro!.Elementos.Should().Contain(t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);

        // Mismo contrato que un documento proyectado antes de que existiera la sede: ambos campos en null.
        var turnoSinSede = respuestaSinFiltro.Elementos.Single(
            t => t.CodigoColaborador == codigoColaborador && t.Fecha == fecha);
        turnoSinSede.Bloques.Should().OnlyContain(b => b.SedeId == null && b.NombreSede == null);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_CombinaSedeIdConCodigoColaborador_CuandoAmbosFiltrosSeAplican()
    {
        // sedeId y codigoColaborador se combinan con AND.
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

        var response = await ConsultarAsync(Filtro(fecha, fecha, codigoColaboradorA, sedeId), ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var respuesta = await response.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        respuesta.Should().NotBeNull();

        respuesta!.Elementos.Should().ContainSingle(t => t.CodigoColaborador == codigoColaboradorA && t.Fecha == fecha);
        respuesta.Elementos.Should().NotContain(t => t.CodigoColaborador == codigoColaboradorB);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_IncluyeSoloALosColaboradoresDeLaLista_CuandoSeFiltraPorCodigosColaborador()
    {
        // Lista OR dentro del campo, AND con la sede, keyset intacto al paginar y lista vacia = sin
        // filtro. Un solo sembrado de tres colaboradores para todas las consultas; A es el unico con sede.
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var fecha = new DateOnly(2026, 6, 8);
        var sedeIdA = Guid.CreateVersion7().ToString();
        var codigoA = Guid.CreateVersion7().ToString();
        var codigoB = Guid.CreateVersion7().ToString();
        var codigoC = Guid.CreateVersion7().ToString();

        await PublicarTurnoConSedeAsync(
            Guid.CreateVersion7(), codigoA, fecha, "[TEST] Turno Lista A", sedeIdA, "[TEST] Sede Lista A");
        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoB, fecha, "[TEST] Turno Lista B");
        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoC, fecha, "[TEST] Turno Lista C");

        (await EsperarTurnoEnLaListaAsync(codigoA, fecha, ct)).Should().BeTrue("A deberia materializarse");
        (await EsperarTurnoEnLaListaAsync(codigoB, fecha, ct)).Should().BeTrue("B deberia materializarse");
        (await EsperarTurnoEnLaListaAsync(codigoC, fecha, ct)).Should().BeTrue("C deberia materializarse");

        var conLista = await ConsultarOkAsync(FiltroConLista(fecha, fecha, codigoA, codigoB), ct);
        conLista.Elementos.Should().Contain(t => t.CodigoColaborador == codigoA && t.Fecha == fecha);
        conLista.Elementos.Should().Contain(t => t.CodigoColaborador == codigoB && t.Fecha == fecha);
        conLista.Elementos.Should().NotContain(t => t.CodigoColaborador == codigoC);

        var conListaYSede = await ConsultarOkAsync(
            new { desdeFecha = fecha, hastaFecha = fecha, codigosColaborador = new[] { codigoA, codigoB }, sedeId = sedeIdA },
            ct);
        conListaYSede.Elementos.Select(t => t.CodigoColaborador).Should().Equal(codigoA);

        // El orden entre A y B depende del collation de Postgres: se afirma que las dos paginas
        // reparten a A y B sin repetir ni perder a ninguno, no cual va primero.
        var pagina1 = await ConsultarOkAsync(
            new { desdeFecha = fecha, hastaFecha = fecha, codigosColaborador = new[] { codigoA, codigoB }, take = 1 }, ct);
        pagina1.SiguienteCursor.Should().NotBeNullOrEmpty();

        var pagina2 = await ConsultarOkAsync(
            new { desdeFecha = fecha, hastaFecha = fecha, codigosColaborador = new[] { codigoA, codigoB }, take = 1, cursor = pagina1.SiguienteCursor },
            ct);
        pagina2.SiguienteCursor.Should().BeNull();
        pagina1.Elementos.Concat(pagina2.Elementos).Select(t => t.CodigoColaborador)
            .Should().BeEquivalentTo([codigoA, codigoB]);

        var conListaVacia = await ConsultarOkAsync(FiltroConLista(fecha, fecha), ct);
        conListaVacia.Elementos.Should().Contain(t => t.CodigoColaborador == codigoC && t.Fecha == fecha);
    }

    private const string TopicCancelacionEntrada = "cancelacion-turno-diario-solicitada";
    private const string TopicAusenciaEntrada = "ausencia-diaria-programada";
    private const string MotivoAusencia = "Vacaciones";

    private sealed record AusenciaListadaSmoke(
        string CodigoColaborador,
        DateOnly Fecha,
        string? MotivoAusencia,
        IReadOnlyList<BloqueSmoke> Bloques);

    private sealed record ListaAusenciasSmoke(IReadOnlyList<AusenciaListadaSmoke> Elementos);

    private async Task PublicarAusenciaAsync(
        string codigoColaborador, DateOnly fecha)
    {
        var ausenciaId = Guid.CreateVersion7();
        await serviceBus.PublishAsync(codigoColaborador, TopicAusenciaEntrada, new
        {
            AusenciaId = ausenciaId,
            Colaborador = new ResumenColaborador(
                "CC-444555666", codigoColaborador, NombreCompletoSembrado),
            Fecha = fecha.ToString("yyyy-MM-dd"),
            Motivo = MotivoAusencia
        }, ausenciaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_ExcluyeElDia_CuandoElTurnoFueCancelado()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 8);

        await PublicarTurnoAsync(
            Guid.CreateVersion7(), codigoColaborador, fecha, "[TEST] Turno A Cancelar Listado");
        (await EsperarTurnoEnLaListaAsync(codigoColaborador, fecha, ct)).Should().BeTrue(
            "la vista deberia materializarse antes de cancelar el turno");

        var solicitudCancelacionId = Guid.CreateVersion7();
        await serviceBus.PublishAsync(
            codigoColaborador,
            TopicCancelacionEntrada,
            new
            {
                SolicitudId = solicitudCancelacionId,
                Colaborador = new ResumenColaborador(
                    "CC-444555666", codigoColaborador, NombreCompletoSembrado),
                Fecha = fecha.ToString("yyyy-MM-dd")
            },
            solicitudCancelacionId.ToString());

        var respuesta = await ConsultarHastaQueAsync(
            Filtro(fecha, fecha, codigoColaborador), body => body.Elementos.Count == 0, ct);

        respuesta.Elementos.Should().BeEmpty("un dia con turno cancelado y sin ausencia no aparece");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_MuestraLaAusenciaSinBloques_CuandoSeFiltraPorColaboradorYLaExcluyePorSede()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var fecha = new DateOnly(2026, 6, 9);
        var sedeId = Guid.CreateVersion7().ToString();

        await PublicarTurnoConSedeAsync(
            Guid.CreateVersion7(), codigoColaborador, fecha, "[TEST] Turno Cubierto Listado",
            sedeId, "[TEST] Sede Ausencia");
        (await EsperarTurnoEnLaListaAsync(codigoColaborador, fecha, ct)).Should().BeTrue(
            "la vista del turno deberia materializarse antes de registrar la ausencia");

        await PublicarAusenciaAsync(codigoColaborador, fecha);

        var filtroColaborador = Filtro(fecha, fecha, codigoColaborador);
        var respuesta = await ConsultarHastaQueAsync(
            filtroColaborador,
            body => body.Elementos.Any(t => t.CodigoColaborador == codigoColaborador && t.Bloques.Count == 0),
            ct);

        respuesta.Elementos.Should().ContainSingle();

        using var response = await ConsultarAsync(filtroColaborador, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        json.Should().NotContain("[TEST] Turno Cubierto Listado",
            "la respuesta no expone el turno que la ausencia cubre");
        var ausencias = JsonSerializer.Deserialize<ListaAusenciasSmoke>(json, JsonOptions)!;
        ausencias.Elementos.Single().MotivoAusencia.Should().Be(MotivoAusencia);
        ausencias.Elementos.Single().Bloques.Should().BeEmpty();

        using var porSede = await ConsultarAsync(Filtro(fecha, fecha, codigoColaborador, sedeId), ct);
        porSede.StatusCode.Should().Be(HttpStatusCode.OK);
        var listaPorSede = await porSede.Content.ReadFromJsonAsync<ListaTurnosVigentesSmoke>(
            JsonOptions, cancellationToken: ct);
        listaPorSede!.Elementos.Should().BeEmpty("un dia de ausencia no tiene bloques y no aparece al filtrar por sede");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarTurnosVigentes_RecorreDosPaginasConSiguienteCursor_CuandoSeEnviaTake()
    {
        Assert.SkipWhen(!serviceBus.IsConfigured,
            "ServiceBus no configurado. Usa appsettings.local.json o variable ServiceBus__ConnectionString.");

        var ct = TestContext.Current.CancellationToken;

        var codigoColaborador = Guid.CreateVersion7().ToString();
        var primerDia = new DateOnly(2026, 6, 10);
        var segundoDia = primerDia.AddDays(1);

        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoColaborador, primerDia, "[TEST] Turno Pagina Uno");
        await PublicarTurnoAsync(Guid.CreateVersion7(), codigoColaborador, segundoDia, "[TEST] Turno Pagina Dos");
        (await EsperarTurnoEnLaListaAsync(codigoColaborador, primerDia, ct)).Should().BeTrue(
            "el primer turno deberia materializarse dentro del timeout");
        (await EsperarTurnoEnLaListaAsync(codigoColaborador, segundoDia, ct)).Should().BeTrue(
            "el segundo turno deberia materializarse dentro del timeout");

        var pagina1 = await ConsultarOkAsync(
            new { desdeFecha = primerDia, hastaFecha = segundoDia, codigosColaborador = new[] { codigoColaborador }, take = 1 }, ct);

        pagina1.Elementos.Select(t => t.Fecha).Should().Equal(primerDia);
        pagina1.SiguienteCursor.Should().NotBeNullOrEmpty();

        var pagina2 = await ConsultarOkAsync(
            new { desdeFecha = primerDia, hastaFecha = segundoDia, codigosColaborador = new[] { codigoColaborador }, take = 1, cursor = pagina1.SiguienteCursor },
            ct);

        pagina2.Elementos.Select(t => t.Fecha).Should().Equal(segundoDia);
        pagina2.SiguienteCursor.Should().BeNull();
    }
}
