// Issue #376 (MEF-ADR-0043 paso 3): smoke tests del endpoint DELETE
// colaboradores/{id}/etiquetas/{categoria} (retirar la etiqueta de una categoria -- remocion veraz,
// sin payload). Reemplaza el POST Colaboradores/Etiquetas/Retiros (issue #355): la ruta vieja deja
// de existir (CA-6), {id} = Identificacion.ToString() ("CC-<numero>", round-trip con
// Identificacion.Parsear, MEF-ADR-0037), MISMA ruta que AsignarEtiqueta (se distinguen por verbo
// HTTP), y sin body -- RetirarEtiquetaValidator (que validaba el body viejo) se elimino, no hay
// nada que deserializar en ese punto. Molde: TerminarVinculacionSmokeTests/
// IniciarVinculacionSmokeTests -- mismo comando event-sourcing puro sin consumidores downstream
// (CA-ADR-0030): sin ServiceBusFixture, la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture.
//
// Arrange: RetirarEtiqueta exige un ColaboradorAggregateRoot existente con la categoria YA
// ASIGNADA -- el arrange de cada test registra el colaborador y asigna (y, cuando aplica, termina
// su vinculacion o inicia una vinculacion nueva, escenario de reingreso issue #378) via los mismos
// comandos que los originan (#330, #349/#379, #378, y AsignarEtiqueta del propio ciclo de vida, ya
// migrado a PUT por este issue), nunca sembrando datos por fuera del API. Issue #379: la
// terminacion ahora exige el {codigo} de la vinculacion en la ruta -- RegistrarColaboradorAsync
// devuelve el codigo (== CodigoColaborador del comando, verificado en
// ColaboradorAggregateRoot.Registrar) para que el arrange lo use como {codigo} al terminar.
//
// Contenido persistido (EtiquetaRetirada, payload plano con solo CategoriaNormalizada -- un campo
// ESCALAR top-level, a diferencia de EtiquetaAsignada): a diferencia de AsignarEtiquetaSmokeTests,
// aqui SI se puede filtrar por (campoJson, valorJson) con el overload estandar de
// PostgresFixture.ExisteEventoAsync/ObtenerEventoAsync, incluso en streams que acumulan mas de un
// evento etiqueta_retirada.
//
// CA-3 (ruta de exito, #355): 202 + el stream recibe etiqueta_retirada con la categoria normalizada,
// retirando por una forma de la URL distinta a la asignada ("área" retira lo asignado como "Area") --
// evidencia black-box de que el direccionamiento por categoria normalizada (CA-4 de #376) tambien
// aplica al retiro.
// CA-4 (rutas de rechazo, decision #2 -- SIN idempotencia silenciosa): categoria nunca asignada, o
// un error de transcripcion sobre una categoria existente ("Aera" vs "Area") -> 409, sin evento
// nuevo, la etiqueta existente (si la hay) queda intacta.
// CA-5 (rutas de rechazo): la ultima vinculacion tiene terminacion registrada -- pasada o un
// preaviso cuya fecha no ha llegado, sin distincion -> 409, sin evento.
// CA-6: la etiqueta pertenecia a la vinculacion ANTERIOR (congelada tras la terminacion) -- la
// vinculacion vigente (el reingreso) no la hereda, asi que retirarla encuentra la categoria
// inexistente -> 409, igual que cualquier categoria nunca asignada.
// CA-7: colaborador inexistente -> 404.
// CA-3 (issue #376): {id} de ruta invalido -- sin guion, tipo fuera de la lista PILA, o numero vacio
// tras el guion -> 400, con Identificacion.Parsear como unico punto de traduccion (precedente
// ObtenerFichaColaborador), sin tocar el event store.
//
// Fuera de alcance (no forma parte de la lista cerrada de CA-3): una {categoria} de ruta vacia no es
// una validacion de aplicacion sino un segmento de ruta ausente -- el propio host/routing de Azure
// Functions decide que hacer con "DELETE .../etiquetas/" (trailing slash), no el codigo del
// endpoint; no hay CA que lo exija y no se testea aqui.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RetirarEtiquetaFunction;

public class RetirarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoEtiquetaAsignada = "etiqueta_asignada";
    private const string TipoEventoEtiquetaRetirada = "etiqueta_retirada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La ausencia de evento tras un rechazo es sincrona (sin proyeccion downstream, event-sourcing
    // puro) -- un timeout corto alcanza para probarla sin alargar la suite (mismo criterio que
    // CorregirNombresSmokeTests.TimeoutAusencia).
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida. El formato "N" en MAYUSCULAS ya es alfanumerico ASCII, asi que
    // sobrevive intacto a la limpieza del numero (#381) y la llave esperada de abajo coincide con
    // la que arma el backend.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): se recompone aqui a mano, no se
    // deriva de Identificacion.ToString(), para que un cambio de formato en el VO no se auto-valide.
    // Separador "-" desde el issue #381. Es EXACTAMENTE el mismo valor que el {id} de ruta del
    // endpoint (round-trip con Identificacion.Parsear, issue #376): se reusa para ambos fines.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Rutas del ciclo de vida migradas por el issue #376: AsignarEtiqueta (PUT, arrange) y
    // RetirarEtiqueta (DELETE, bajo prueba) comparten el mismo template de ruta.
    private static string RutaEtiqueta(string id, string categoria) =>
        $"/api/colaboradores/{id}/etiquetas/{Uri.EscapeDataString(categoria)}";

    private static object PayloadRegistro(string numeroIdentificacion, DateOnly fechaInicio, string codigoColaborador) => new
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

    // Body reducido a los 2 campos que no se derivan de la ruta (issue #378): CodigoColaborador +
    // FechaInicio.
    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    // Body reducido de AsignarEtiqueta (issue #376, arrange de este archivo): solo Valor.
    private static object PayloadValor(string valor) => new { valor };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API. Devuelve el codigo de la
    // vinculacion inicial (== CodigoColaborador del comando, verificado en
    // ColaboradorAggregateRoot.Registrar) para que el arrange lo use como {codigo} de ruta al
    // terminar (issue #379).
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");

        return codigo;
    }

    // Arrange comun: asigna la etiqueta que luego se intenta retirar -- via el comando que la
    // origina (AsignarEtiqueta, ya migrado a PUT colaboradores/{id}/etiquetas/{categoria} por este
    // mismo issue), nunca sembrando el event store por fuera del API.
    private async Task AsignarEtiquetaAsync(
        string id, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), PayloadValor(valor), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
    }

    // Arrange comun (CA-5): cierra la vinculacion vigente -- via el comando que la origina
    // (#349/#379), nunca sembrando el event store por fuera del API. Issue #379: la ruta gano el
    // {codigo} -- ya no es "/api/Colaboradores/Terminaciones" con identificacion en el body.
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

    // Arrange comun (CA-6): inicia una vinculacion nueva sobre el colaborador tras una terminacion
    // -- escenario de negocio de reingreso -- via el comando que lo origina (issue #378, reemplaza
    // a ReingresarColaborador #350), nunca sembrando el event store por fuera del API.
    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    private Task<HttpResponseMessage> RetirarEtiquetaAsync(
        string id, string categoria, CancellationToken ct) =>
        _client.DeleteAsync(RutaEtiqueta(id, categoria), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-3 (#355): camino feliz -- retirar por una forma de la URL distinta de la que se asigno
    // ("área" retira lo asignado como "Area", misma categoria normalizada, CA-4 de #376) -> 202 y el
    // stream recibe etiqueta_retirada con la categoria normalizada. Sin Service Bus (event-sourcing
    // puro): mt_events es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna202YPersisteEtiquetaRetirada_CuandoLaRutaLlegaConCategoriaEnOtraForma()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, Timeout,
            campoJson: "CategoriaNormalizada", valorJson: "area");

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaRetirada} con CategoriaNormalizada 'area' deberia existir en el stream {id}");
    }

    // CA-4 (decision #2, sin idempotencia silenciosa): retirar una categoria que nunca se asigno
    // -> 409, sin evento nuevo. No requiere Postgres: el status code ya prueba que el aggregate
    // declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoCategoriaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4 (el typo debe aflorar, decision #2 del issue #355): "Aera" no es "Area" -- categorias
    // distintas normalizadas, aunque exista una etiqueta para "Area" -> 409 igual, ninguna
    // etiqueta_retirada nueva; la etiqueta existente ("Area") queda intacta (el conteo de
    // etiqueta_asignada se mantiene en 1).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoHayUnErrorDeTranscripcionEnLaCategoriaDeLaRuta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 5), ct);
        await AsignarEtiquetaAsync(id, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(id, "Aera", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "un error de transcripcion en la categoria no deberia persistir un etiqueta_retirada");

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);
        asignaciones.Should().Be(1,
            "la etiqueta original ('Area') deberia quedar intacta -- el rechazo no la toca");
    }

    // CA-5 (regla estricta de apertura): la ULTIMA vinculacion tiene terminacion registrada -> 409,
    // sin evento nuevo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- las
    // etiquetas describen la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, fechaPreavisoFutura, ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-6 (reingreso nace limpio): la etiqueta pertenecia a la vinculacion ANTERIOR (congelada
    // tras la terminacion) -- la vinculacion vigente (el reingreso) no la hereda, asi que
    // retirarla encuentra la categoria inexistente -> 409, igual que cualquier categoria nunca
    // asignada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoEtiquetaPerteneceALaVinculacionAnteriorTrasReingreso()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);
        await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-7: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-3 (issue #376): {id} de ruta sin guion -> 400, Identificacion.Parsear como unico punto de
    // traduccion (precedente ObtenerFichaColaborador.FunctionEndpoint).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion(); // p.ej. "3F2A0C..." sin "CC-" adelante

        var response = await RetirarEtiquetaAsync(idSinGuion, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: tipo de identificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) dentro del
    // {id} de ruta -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoTipoDeLaIdentificacionEnLaRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = $"XX-{NuevoNumeroIdentificacion()}";

        var response = await RetirarEtiquetaAsync(id, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400 (Identificacion.Crear rechaza un
    // numero vacio tras la limpieza).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoNumeroDeLaIdentificacionEnLaRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        const string idConNumeroVacio = "CC-";

        var response = await RetirarEtiquetaAsync(idConNumeroVacio, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
