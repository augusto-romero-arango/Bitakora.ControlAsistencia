// Issue #356 (creacion) / issue #386 (esta revision): smoke tests de ObtenerFichaColaborador, GET
// colaboradores/fichas/{id}. Function GET read-side sobre la proyeccion
// FichaColaborador (receta N1, MEF-ADR-0034/0035): la primera vista materializada del dominio
// Colaboradores, consultable puntualmente por identificacion y base del flujo de reingreso (por eso
// la consulta puntual INCLUYE no-vigentes, a diferencia de un futuro listado).
//
// Issue #386: {id} = Identificacion.ToString() ("CC-79543210") -- un unico segmento, la misma llave
// que devuelve FichaColaborador.Id y la misma forma de id que ya usan los comandos del ciclo de vida
// (#376/#377). Cierra el round-trip del cliente: el Id que la API devuelve se reusa tal cual en
// cualquier URL, sin que el cliente lo parta jamas. El endpoint lo parsea UNA sola vez con
// Identificacion.Parsear (punto unico de conversion, MEF-ADR-0037 seccion 2) -- de ahi el 400 de
// CA-3 y la normalizacion de CA-4. La ruta vieja de dos segmentos
// ({tipoIdentificacion}/{numero}) deja de existir: CA-5 lo verifica AFIRMATIVAMENTE contra el
// entorno real (404 del host), no solo por ausencia de referencias en este archivo -- misma
// tecnica que CorregirNombresSmokeTests (#377), porque "no la llama nadie" no distingue una ruta
// eliminada de una que sigue viva.
//
// Arrange via API, nunca sembrando el event store por fuera de ella: el colaborador se crea con
// POST Colaboradores (#330) y, cuando aplica, se termina su vinculacion con POST
// colaboradores/{id}/vinculaciones/{codigo}:terminar (#349/#379) -- los mismos comandos que #356
// usa como fuente de eventos para la proyeccion.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaColaborador DESPUES de que
// Colaboradores persiste sus eventos. Los casos de exito envuelven la consulta en
// Polling.WaitUntilAsync (timeout estandar 30s) -- unica excepcion documentada al "no usar Polling
// directo en tests": si el timeout se agota es un fallo real (worker no desplegado o proyeccion sin
// registrar en el named store), nunca un skip.
//
// Estos tests quedan ROJOS hasta que el deploy publique la ruta nueva en dev: mientras la revision
// anterior siga corriendo, solo existe la ruta vieja de dos segmentos y el host responde 404 a todo
// lo demas -- los casos 400 fallan y los casos 404 pasan por la razon equivocada (mismo precedente
// que ObtenerTurnoVigenteSmokeTests en ControlHoras y que CorregirNombresSmokeTests tras el rename
// de #377). El CI de PR no los ejecuta (solo corre *.Tests); su veredicto real se lee despues del
// deploy.
//
// Formas locales DESACOPLADAS del read model de produccion
// (Bitakora.ControlAsistencia.ReadModels.Colaboradores.FichaColaborador/EtiquetaFicha): el smoke
// test no referencia ReadModels (isla, MEF-ADR-0034 seccion 5) ni el Function App.
// FichaColaboradorRespuestaSmoke replica solo el shape de la respuesta HTTP (DTO propio del
// endpoint, ObtenerFichaColaborador.FichaColaboradorRespuesta), no el read model.
//
// No se repite aqui el detalle de upsert/normalizacion de etiquetas (CA-4), ni el mapeo exhaustivo
// de reingreso (CA-5): esas reglas de negocio de la proyeccion ya las cubre el unit test de
// FichaColaboradorProjection (projection-test-writer). Este smoke test es black-box: solo verifica
// que el endpoint desplegado responde con la vista materializada y que el contrato HTTP propio del
// endpoint (CA-6: centinela oculto, no-vigentes incluidos, bordes 404/400) se cumple contra el
// entorno real.
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
    // JsonNamingPolicy.CamelCase para las respuestas HTTP), mientras que las formas locales de este
    // archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record EtiquetaFichaSmoke(string Categoria, string Valor);

    private sealed record FichaColaboradorRespuestaSmoke(
        string Id,
        string NombreCompleto,
        string CodigoColaborador,
        DateOnly VigenteDesde,
        DateOnly? VigenteHasta,
        IReadOnlyList<EtiquetaFichaSmoke> Etiquetas,
        IReadOnlyDictionary<string, string> EtiquetasNormalizadas);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream (y por lo tanto el Id de la ficha) es Identificacion.ToString()
    // ("CC-<numero>"), no un Guid nuevo por llamada.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): mismo formato que
    // ColaboradorAggregateRoot.ComputarStreamId (separador "-" desde #381), reconstruido localmente
    // -- el smoke test no referencia el Function App (Colaboradores.Entities).
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // El {id} que un cliente real pone en la URL (issue #386). Deliberadamente separado de
    // ComputarStreamId, mismo criterio que CorregirNombresSmokeTests (#377): uno es la ENTRADA de la
    // request, el otro el ORACULO del Id que la respuesta debe traer -- que hoy coincidan
    // textualmente es justamente lo que estos tests prueban (el round-trip del cliente), no algo que
    // puedan asumir compartiendo el mismo metodo.
    private static string IdDeRuta(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string Ruta(string id) => $"/api/colaboradores/fichas/{id}";

    // Ruta vieja de dos segmentos (issue #356), eliminada por #386 -- solo la usa el test de CA-5.
    private static string RutaVieja(string tipoIdentificacion, string numero) =>
        $"/api/colaboradores/fichas/{tipoIdentificacion}/{numero}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre = "[TEST]",
            segundoNombre = (string?)null,
            primerApellido = "Smoke",
            segundoApellido = (string?)null,
            codigoColaborador,
            fechaInicio
        };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigoColaborador), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun (CA-6, "INCLUYE no-vigentes"): cierra la vinculacion vigente -- via el comando
    // que la origina (#349/#379), nunca sembrando el event store por fuera del API. Issue #379: la
    // ruta gano el {codigo} -- ya no es "/api/Colaboradores/Terminaciones" con identificacion en el
    // body.
    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Act comun de los caminos felices: reintenta el GET hasta que la proyeccion asincrona
    // materialice la ficha (404 = el worker todavia no la aplico) y, cuando se pasa "hasta", hasta
    // que el body ademas cumpla esa condicion (el segundo evento del stream ya aplicado). Devuelve
    // un valor no nulo o lanza TimeoutException -- por eso ningun caller afirma NotBeNull.
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

    // CA-1/CA-6 (camino feliz, vinculacion abierta): registrar un colaborador nuevo materializa la
    // ficha con Id, NombreCompleto y CodigoColaborador de la vinculacion, y CA-6 exige que el
    // centinela de vigencia abierta (9999-12-31) jamas salga por la API -- VigenteHasta debe llegar
    // vacio (null), nunca la fecha centinela.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna200ConVigenteHastaVacio_CuandoLaVinculacionEstaAbierta()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);
        var codigoColaborador = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, codigoColaborador, ct);

        // Act + Assert: reintentar el GET hasta que la proyeccion asincrona materialice la ficha.
        var respuesta = await EsperarFichaAsync(IdDeRuta(numeroIdentificacion), ct);

        // Sin assert de NotBeNull: WaitUntilAsync devuelve un valor no nulo o lanza TimeoutException
        // ("el worker no materializo FichaColaborador dentro del timeout"), nunca null.
        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.NombreCompleto.Should().Be("[TEST] Smoke");
        respuesta.CodigoColaborador.Should().Be(codigoColaborador);
        respuesta.VigenteDesde.Should().Be(fechaInicio);
        respuesta.VigenteHasta.Should().BeNull(
            "el centinela de vigencia abierta (9999-12-31) jamas debe salir por la API (CA-6)");
        respuesta.Etiquetas.Should().BeEmpty();
        respuesta.EtiquetasNormalizadas.Should().BeEmpty();
    }

    // CA-2/CA-6 (INCLUYE no-vigentes): tras terminar la vinculacion, la consulta puntual sigue
    // respondiendo 200 -- a diferencia de un futuro listado filtrado por vigencia, este endpoint es
    // la base del flujo de reingreso y necesita ver la ficha aunque este cerrada. VigenteHasta debe
    // reflejar la fecha efectiva real de la terminacion, nunca el centinela ni vacio.
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

        // Act + Assert: reintentar hasta que el worker aplique TAMBIEN VinculacionTerminada -- un
        // 200 con VigenteHasta todavia nulo solo significa que la proyeccion aun no proceso el
        // segundo evento del stream.
        var respuesta = await EsperarFichaAsync(
            IdDeRuta(numeroIdentificacion), ct, hasta: ficha => ficha.VigenteHasta is not null);

        respuesta.Id.Should().Be(ComputarStreamId(numeroIdentificacion));
        respuesta.VigenteHasta.Should().Be(fechaEfectiva);
    }

    // CA-4 (issue #386): el {id} en minusculas resuelve la MISMA ficha que su forma canonica --
    // Identificacion.Parsear normaliza tipo (TipoIdentificacion.Desde: trim + MAYUSCULAS) y numero
    // (Crear: limpieza + MAYUSCULAS) antes de componer la llave, asi que el cliente nunca tiene que
    // preocuparse por el casing del id que la propia API le devolvio. Es la garantia que un route
    // constraint NO daria (MEF-ADR-0037 seccion 2: los constraints no normalizan casing), y la
    // razon por la que este endpoint parsea en vez de reenviar el segmento crudo a LoadAsync: sin
    // el parseo, la comparacion de igualdad de texto de Postgres (collation deterministica) haria
    // que "cc-..." simplemente no encuentre nada.
    //
    // Se espera primero con el id canonico (prueba que la ficha se materializo) y recien despues se
    // consulta en minusculas con un unico GET: asi un fallo distingue "el worker no proyecto" de
    // "la normalizacion se rompio", en vez de dar un timeout ambiguo.
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

    // CA-2/CA-6: ficha inexistente -> 404 sin body -- distingue el NotFoundResult() del endpoint de
    // un 404 con payload de error, y de la pagina de error del host si la ruta no existiera.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        // Numero nunca registrado por ningun test -- no puede tener ficha materializada.
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await _client.GetAsync(Ruta(IdDeRuta(numeroIdentificacion)), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    // CA-3: tipo de identificacion del {id} fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) ->
    // 400 -- TipoIdentificacion.Desde rechaza dentro de Identificacion.Parsear y el endpoint lo
    // traduce a BadRequest en su unico punto de traduccion (borde HTTP tipado, MEF-ADR-0037).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"XX-{NuevoNumeroIdentificacion()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: {id} sin guion -> 400. Contra el entorno real importa distinguirlo del 404 del host: el
    // id llega a la Function (la ruta existe) y es el parseo quien lo rechaza.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(
            Ruta($"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: numero vacio tras el guion del {id} -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaColaborador_Retorna400_CuandoElNumeroDelIdDeRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"{TipoIdentificacionCc}-"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-5: la ruta vieja de dos segmentos deja de existir. Es la unica verificacion AFIRMATIVA de
    // ese criterio -- el resto de la suite lo cubre solo por ausencia de referencias, que no
    // distingue "la ruta se elimino" de "sigue viva y nadie la llama". El rename esta pactado en el
    // issue (MEF-ADR-0043 seccion 7, por analogia): si alguien reintrodujera la ruta vieja "por
    // compatibilidad", este test lo delata contra el entorno real, que es donde el breaking change
    // se paga.
    //
    // El arrange (registrar + esperar la ficha) es lo que hace la verificacion NO vacua: se llama la
    // ruta vieja con una identificacion que SI tiene ficha materializada, asi que un 404 solo puede
    // significar que ninguna Function enruta esos cuatro segmentos. Sin el arrange, la ruta vieja
    // seguiria respondiendo 404 aunque estuviera viva (ficha inexistente) y el test pasaria por la
    // razon equivocada.
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
