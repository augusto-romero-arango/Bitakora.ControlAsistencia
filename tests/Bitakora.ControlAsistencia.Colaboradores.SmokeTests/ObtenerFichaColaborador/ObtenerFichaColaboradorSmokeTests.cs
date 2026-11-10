using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ObtenerFichaColaborador;

public class ObtenerFichaColaboradorSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Formas locales desacopladas: replican el shape del DTO de respuesta, no el read model; el smoke no
    // referencia ReadModels ni el Function App (MEF-ADR-0034 seccion 5).
    private sealed record EtiquetaFichaSmoke(string Categoria, string Valor);

    private sealed record FichaColaboradorRespuestaSmoke(
        string Id,
        string NombreCompleto,
        string CodigoColaborador,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta,
        IReadOnlyList<EtiquetaFichaSmoke> Etiquetas,
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas,
        string? CodigoSede = null,
        Guid? JornadaId = null);

    private sealed record DirectorioJornadaSmoke(string Identificacion, Guid? JornadaId);

    private sealed record DirectorioSmoke(List<DirectorioJornadaSmoke> Elementos);

    // Unico por test: el Id de la ficha es la identificacion, no un Guid por llamada.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002), reconstruido a mano: el smoke no referencia el Function App.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Separado de ComputarStreamId a proposito: que la entrada y el Id de la respuesta coincidan (el
    // round-trip del cliente) es lo que se prueba.
    private static string IdDeRuta(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static readonly HttpMethod MetodoQuery = new("QUERY");

    private static string Ruta(string id) => $"/api/colaboradores/fichas/{id}";

    private static string RutaVieja(string tipoIdentificacion, string numero) =>
        $"/api/colaboradores/fichas/{tipoIdentificacion}/{numero}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador, string? codigoSede = null) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador,
            fechaInicio,
            codigoSede
        };

    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador, CancellationToken ct,
        string? codigoSede = null)
    {
        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigoColaborador, codigoSede), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Reemplazo completo: la misma ruta sirve para la primera asignacion y la reasignacion.
    private async Task AsignarSedeAsync(string id, string codigoSede, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/colaboradores/{id}/sede", new { codigoSede }, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarSede funcione");
    }

    // codigoSede null es el caso "reingreso nace limpio".
    private async Task IniciarVinculacionAsync(
        string id, string codigoColaborador, DateOnly fechaInicio, CancellationToken ct, string? codigoSede = null)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            new { codigoColaborador, fechaInicio, codigoSede },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // 404 = el worker todavia no aplico la ficha. Con "hasta" espera ademas a que el body la cumpla (un
    // evento posterior ya aplicado). Devuelve no nulo o lanza TimeoutException: nadie afirma NotBeNull.
    private Task<FichaColaboradorRespuestaSmoke> EsperarFichaAsync(
        string id, CancellationToken ct, Func<FichaColaboradorRespuestaSmoke, bool>? hasta = null) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(id), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<FichaColaboradorRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);

            return body is not null && (hasta is null || hasta(body)) ? body : null;
        }, Timeout);

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
    public async Task ObtenerFichaColaborador_Retorna200ConVigenteHastaVacio_CuandoLaVinculacionEstaAbierta()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, codigoColaborador, ct);

        var respuesta = await EsperarFichaAsync(IdDeRuta(numeroIdentificacion), ct);

        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.NombreCompleto.Should().Be("[TEST] Smoke");
        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.VigenteDesde.Should().Be(fechaInicio);
        respuesta.VigenteHasta.Should().BeNull(
            "el centinela de vigencia abierta (9999-12-31) jamas debe salir por la API (CA-6)");
        respuesta.Etiquetas.Should().BeEmpty();
        respuesta.EtiquetasNormalizadas.Should().BeEmpty();
        respuesta.CodigoSede.Should().BeNull(
            "issue #519 CA-2: RegistrarColaborador sin codigoSede deja la ficha sin sede");
    }

    // La consulta puntual INCLUYE no-vigentes: es la base del flujo de reingreso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna200ConVigenteHastaReal_CuandoLaVinculacionEstaTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 2, 1);
        var fechaEfectiva = new DateOnly(2026, 6, 30);
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, codigoColaborador, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoColaborador, fechaEfectiva, ct);

        // Un 200 con VigenteHasta nulo solo significa que la proyeccion aun no aplico la terminacion.
        var respuesta = await EsperarFichaAsync(
            IdDeRuta(numeroIdentificacion), ct, hasta: ficha => ficha.VigenteHasta is not null);

        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.VigenteHasta.Should().Be(fechaEfectiva);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_ExponeCodigoSede_CuandoElRegistroIncluyeSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 4, 15), NuevoCodigoColaborador(), ct, codigoSede: "BOG");

        var respuesta = await EsperarFichaAsync(
            IdDeRuta(numeroIdentificacion), ct, hasta: ficha => ficha.CodigoSede is not null);

        respuesta.CodigoSede.Should().Be("BOG");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_ExponeCodigoSedeReemplazada_CuandoSeAsignaYLuegoSeReasignaLaSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 4, 20), NuevoCodigoColaborador(), ct);
        await EsperarFichaAsync(id, ct);

        await AsignarSedeAsync(id, "BOG", ct);
        await EsperarFichaAsync(id, ct, hasta: ficha => ficha.CodigoSede == "BOG");

        await AsignarSedeAsync(id, "MED", ct);
        var respuesta = await EsperarFichaAsync(id, ct, hasta: ficha => ficha.CodigoSede == "MED");

        respuesta.CodigoSede.Should().Be("MED");
    }

    // A diferencia del registro inicial, aqui SI hay una sede previa que perder.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_QuedaSinCodigoSede_CuandoElReingresoNoTraeSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var codigoColaborador = NuevoCodigoColaborador();
        var fechaReingreso = new DateOnly(2026, 7, 1);

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 4, 25), codigoColaborador, ct, codigoSede: "BOG");
        await EsperarFichaAsync(id, ct, hasta: ficha => ficha.CodigoSede is not null);

        await TerminarVinculacionAsync(id, codigoColaborador, new DateOnly(2026, 6, 30), ct);
        await EsperarFichaAsync(id, ct, hasta: ficha => ficha.VigenteHasta is not null);

        await IniciarVinculacionAsync(id, NuevoCodigoColaborador(), fechaReingreso, ct);
        var respuesta = await EsperarFichaAsync(id, ct, hasta: ficha => ficha.VigenteDesde == fechaReingreso);

        respuesta.CodigoSede.Should().BeNull(
            "el reingreso sin codigoSede debe limpiar la sede de la vinculacion anterior");
    }

    private Task<DirectorioJornadaSmoke> EsperarDirectorioAsync(
        string id, CancellationToken ct, Func<DirectorioJornadaSmoke, bool> hasta) =>
        Polling.WaitUntilAsync(async () =>
        {
            using var request = new HttpRequestMessage(MetodoQuery, "/api/colaboradores/directorio")
            {
                Content = JsonContent.Create(new { identificaciones = new[] { id } })
            };
            var response = await _client.SendAsync(request, ct);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var lista = await response.Content.ReadFromJsonAsync<DirectorioSmoke>(
                JsonOptions, cancellationToken: ct);
            var entrada = lista?.Elementos.FirstOrDefault(e => e.Identificacion == id);
            return entrada is not null && hasta(entrada) ? entrada : null;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_DebeMostrarLaJornadaAsignada_CuandoSeLeAsignoUnaJornada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var jornadaId = Guid.CreateVersion7();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 5, 4), NuevoCodigoColaborador(), ct);

        var fichaInicial = await EsperarFichaAsync(id, ct);
        fichaInicial.JornadaId.Should().BeNull("un colaborador recien registrado no tiene Jornada asignada");
        var directorioInicial = await EsperarDirectorioAsync(id, ct, _ => true);
        directorioInicial.JornadaId.Should().BeNull();

        var asignacion = await _client.PutAsJsonAsync($"/api/colaboradores/{id}/jornada", new { jornadaId }, ct);
        asignacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AsignarJornada funcione");

        var ficha = await EsperarFichaAsync(id, ct, hasta: f => f.JornadaId == jornadaId);
        var directorio = await EsperarDirectorioAsync(id, ct, e => e.JornadaId == jornadaId);

        ficha.JornadaId.Should().Be(jornadaId);
        directorio.JornadaId.Should().Be(jornadaId);
    }

    // Identificacion.Parsear normaliza tipo y numero a MAYUSCULAS antes de componer la llave: sin ese
    // parseo, la igualdad de texto de Postgres haria que "cc-..." no encuentre nada (un route constraint
    // no normaliza casing, MEF-ADR-0037 seccion 2). Se espera con el id canonico y luego un unico GET en
    // minusculas, para que un fallo distinga "el worker no proyecto" de "se rompio la normalizacion".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna200ConLaMismaFicha_CuandoElIdDeRutaViajaEnMinusculas()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 3, 10), codigoColaborador, ct);
        await EsperarFichaAsync(IdDeRuta(numeroIdentificacion), ct);

        var response = await _client.GetAsync(
            Ruta(IdDeRuta(numeroIdentificacion).ToLowerInvariant()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var respuesta = await response.Content.ReadFromJsonAsync<FichaColaboradorRespuestaSmoke>(
            JsonOptions, cancellationToken: ct);

        respuesta!.Id.Should().Be(ComputarStreamId(numeroIdentificacion),
            "el id se normaliza al canonico antes de tocar Marten, y la respuesta siempre trae esa forma");
        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
    }

    // Sin body: distingue el NotFoundResult() del endpoint de un 404 con payload y de la pagina del host.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await _client.GetAsync(Ruta(IdDeRuta(numeroIdentificacion)), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"XX-{NuevoNumeroIdentificacion()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Importa distinguirlo del 404 del host: la ruta existe y es el parseo quien rechaza el id.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            Ruta($"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElNumeroDelIdDeRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"{TipoIdentificacionCc}-"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Afirmativo contra el entorno: la ausencia de referencias no distingue "la ruta se elimino" de "sigue
    // viva y nadie la llama". El arrange evita que sea vacuo: esta identificacion SI tiene ficha, asi que
    // la ruta vieja, si siguiera viva, responderia 200 y no 404.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna404DelHost_CuandoSeLlamaLaRutaViejaDeDosSegmentos()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 4, 5), NuevoCodigoColaborador(), ct);
        await EsperarFichaAsync(IdDeRuta(numeroIdentificacion), ct);

        var response = await _client.GetAsync(
            RutaVieja(TipoIdentificacionCc, numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "colaboradores/fichas/{tipoIdentificacion}/{numero} se reemplazo por colaboradores/fichas/{id} (issue #386), " +
            "y esta identificacion SI tiene ficha materializada -- si la ruta vieja siguiera viva responderia 200");
    }
}
