// Issue #376 (MEF-ADR-0043 paso 2): smoke tests del endpoint PUT
// colaboradores/{id}/etiquetas/{categoria} (asignar o sobrescribir por completo la etiqueta de una
// categoria -- reemplazo del VO atomico Etiqueta, direccionable por categoria). Reemplaza el POST
// Colaboradores/Etiquetas (issue #355): la ruta vieja deja de existir (CA-6), {id} = Identificacion
// ToString() ("CC-<numero>", round-trip con Identificacion.Parsear, MEF-ADR-0037) y el body se
// reduce a { "valor": "..." }. Molde: TerminarVinculacionSmokeTests/IniciarVinculacionSmokeTests
// -- mismo comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin
// ServiceBusFixture, la unica verificacion black-box de los efectos del handler es leer mt_events
// via PostgresFixture.
//
// Arrange: AsignarEtiqueta exige un ColaboradorAggregateRoot existente -- el arrange de cada test
// registra el colaborador y, cuando aplica, termina su vinculacion (issue #379, ruta con {codigo})
// o inicia una vinculacion nueva (escenario de reingreso, issue #378) via los mismos comandos que
// los originan (#330, #349/#379, #378), nunca sembrando datos por fuera del API. Issue #379: la
// terminacion ahora exige el {codigo} de la vinculacion en la ruta -- RegistrarColaboradorAsync
// devuelve el codigo (== CodigoColaborador del comando, verificado en
// ColaboradorAggregateRoot.Registrar) para que el arrange lo use como {codigo} al terminar.
//
// Contenido persistido (Etiqueta, un VO con ctor privado, #353): se verifica deserializando el
// campo "Etiqueta" con la SERIALIZACION REAL de produccion -- Etiqueta +
// ConfiguracionSerializacionColaboradores.CrearOpcionesMarten() (referenciadas desde
// Colaboradores.DomainEvents, ya cableado en el .csproj por el domain-scaffolder). La comparacion
// es por igualdad de valor (Etiqueta.Equals, #353), NUNCA contra el texto JSON persistido (mt_events
// .data es jsonb; docs 8.14.1). Solo es posible en streams que acumulan UN SOLO evento
// etiqueta_asignada (ObtenerEventoAsync sin filtro trae el primero por seq_id -- ver el comentario
// de PostgresFixture.ObtenerEventoAsync sobre campos objeto): cuando un escenario acumula DOS
// eventos de este tipo en el mismo stream (sobrescritura, reingreso), el efecto se verifica por
// CONTEO (ContarEventosAsync) en vez de contenido -- el detalle exhaustivo de "un valor por
// categoria, nunca duplica" en el diccionario rehidratado ya lo cubre
// AsignarEtiquetaCommandHandlerTests (*.Tests, unit), no se duplica aqui.
//
// CA-1 (ruta de exito): 202 + el stream recibe etiqueta_asignada con la doble forma (original y
// normalizada) de categoria y valor.
// CA-2/CA-4 (rutas de exito): la categoria de la URL se normaliza con Etiqueta.NormalizarCategoria
// -- PUT .../etiquetas/Área y .../etiquetas/area direccionan la MISMA etiqueta (EsMismaCategoria),
// asi que un valor distinto sobrescribe (un evento nuevo se agrega, conteo pasa de 1 a 2); una
// etiqueta identica por valor -> 202 sin evento nuevo (idempotencia silenciosa, conteo se mantiene
// en 1).
// CA-5 (rutas de rechazo): la ultima vinculacion tiene terminacion registrada -- pasada o un
// preaviso cuya fecha no ha llegado, sin distincion -> 409, sin evento.
// CA-6: tras un reingreso, la vinculacion nueva no hereda las etiquetas de la anterior -- asignar
// sobre la vinculacion vigente crea la categoria desde cero -> 202.
// CA-7: colaborador inexistente -> 404.
// CA-3 (issue #376): {id} de ruta invalido -- sin guion, tipo fuera de la lista PILA, o numero vacio
// tras el guion -> 400, con Identificacion.Parsear como unico punto de traduccion (precedente
// ObtenerFichaColaborador), sin tocar el event store.
// Body invalido (Valor vacio) -> 400 via AsignarEtiquetaBodyValidator.
//
// Fuera de alcance (no forma parte de la lista cerrada de CA-3): una {categoria} de ruta vacia no es
// una validacion de aplicacion sino un segmento de ruta ausente -- el propio host/routing de Azure
// Functions decide que hacer con "PUT .../etiquetas/" (trailing slash), no el codigo del endpoint;
// no hay CA que lo exija y no se testea aqui.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AsignarEtiquetaFunction;

public class AsignarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoEtiquetaAsignada = "etiqueta_asignada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Segunda lectura del mismo evento que ExisteEventoAsync ya espero: si el primer polling
    // termino, el evento esta -- no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions OpcionesMarten =
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

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

    // Ruta del endpoint migrado (issue #376): colaboradores/{id}/etiquetas/{categoria}. La
    // categoria viaja cruda en la URL (puede traer tildes, ej. "Área") -- se URL-encodea al
    // construir la ruta, mismo criterio que ListarTurnosVigentesSmokeTests (ControlHoras) con sus
    // query params. El {id} ya es URL-safe por construccion (issue #381: separador "-", numero
    // limpio [A-Z0-9]), asi que no requiere escape.
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

    // Body reducido (issue #376): TipoIdentificacion/NumeroIdentificacion/Categoria ya no viajan
    // aqui -- se derivan de la ruta.
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

    private Task<HttpResponseMessage> AsignarEtiquetaAsync(
        string id, string categoria, string valor, CancellationToken ct) =>
        _client.PutAsJsonAsync(RutaEtiqueta(id, categoria), PayloadValor(valor), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta + categoria nueva -> 202 y el
    // stream recibe etiqueta_asignada con la doble forma (original y normalizada) de categoria y
    // valor. Sin Service Bus (event-sourcing puro): mt_events es la unica ventana black-box a lo
    // que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202YPersisteEtiquetaAsignada_CuandoCategoriaEsNueva()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} deberia existir en el stream {id}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, TimeoutLecturaConfirmada);

        var etiquetaPersistida = eventoPersistido.GetProperty("Etiqueta").Deserialize<Etiqueta>(OpcionesMarten);

        etiquetaPersistida.Should().Be(Etiqueta.Crear("Área", "Tecnología"));
    }

    // CA-2/CA-4: la URL direcciona por categoria NORMALIZADA (EsMismaCategoria: "Área" sobre
    // "area") -- un valor distinto sobrescribe: un evento nuevo se agrega (conteo pasa de 1 a 2), a
    // diferencia de la idempotencia silenciosa (ver el siguiente test). El detalle de que el
    // diccionario rehidratado conserve UN SOLO valor por categoria (no dos) ya lo cubre
    // AsignarEtiquetaCommandHandlerTests a nivel unitario -- este smoke test solo verifica el
    // efecto observable black-box: el endpoint acepta la sobrescritura via una ruta con otra forma
    // de la categoria y persiste otro evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202YAgregaOtroEvento_CuandoLaRutaLlegaConCategoriaEnOtraFormaYValorDistinto()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 20), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(id, "area", "Medellín", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {id}");

        // Misma categoria normalizada, forma cruda distinta en la URL ("Área" vs "area").
        var segundaAsignacion = await AsignarEtiquetaAsync(id, "Área", "Bogotá", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(2,
            "PUT .../etiquetas/Área y .../etiquetas/area deberian direccionar la misma etiqueta (CA-4): sobrescribir con un valor distinto agrega un evento nuevo");
    }

    // CA-2: la etiqueta del comando es IGUAL por valor (Etiqueta.Equals, #353) a la ya asignada
    // para esa categoria -> idempotencia silenciosa: ningun evento nuevo, el conteo se mantiene en
    // 1. La ruta de la segunda request llega con otra forma de la categoria (mismo direccionamiento
    // normalizado, CA-4).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202SinNuevoEvento_CuandoEtiquetaEsIdenticaPorValorALaExistente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 25), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {id}");

        // Misma etiqueta por valor, con otra combinacion de mayusculas/tildes en la ruta y el body.
        var segundaAsignacion = await AsignarEtiquetaAsync(id, "area", "tecnologia", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(1,
            "una etiqueta identica por valor a la existente no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-5 (regla estricta de apertura): la ULTIMA vinculacion tiene terminacion registrada -> 409,
    // sin evento nuevo. No requiere Postgres: el status code ya prueba que el aggregate declino con
    // resultado y el handler lo tradujo (CA-ADR-0030; MEF-ADR-0043 seccion 2 paso 2: el 409 de un
    // PUT es una instancia mas de "declinar con resultado", RFC 9110 §9.3.4).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 5, 1), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5 (preaviso no vencido): un preaviso con fecha futura ya registrado bloquea igual -- las
    // etiquetas describen la relacion laboral ACTIVA, sin importar si la fecha efectiva ya paso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna409_CuandoTerminacionEsUnPreavisoConFechaFutura()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(ComputarStreamId(numeroIdentificacion), codigo, fechaPreavisoFutura, ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-6 (reingreso nace limpio): tras un reingreso, la vinculacion nueva no hereda las etiquetas
    // de la anterior -- asignar sobre la vinculacion vigente crea la categoria desde cero, sin
    // colisionar con la etiqueta congelada de la vinculacion previa. El stream acumula 2 eventos
    // etiqueta_asignada (el de la vinculacion anterior + el de la nueva): se verifica por conteo,
    // mismo criterio que la sobrescritura (ver comentario del encabezado del archivo).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202_CuandoVinculacionEsUnReingresoTrasTerminacionConEtiquetasPrevias()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = ComputarStreamId(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);

        var asignacionPrevia = await AsignarEtiquetaAsync(id, "Área", "Ventas", ct);
        asignacionPrevia.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la asignacion previa al reingreso funcione");

        await TerminarVinculacionAsync(
            ComputarStreamId(numeroIdentificacion), codigo, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, id, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(2,
            "la vinculacion nueva del reingreso deberia aceptar la asignacion como una categoria nueva, agregando otro evento sin colisionar con la etiqueta congelada de la vinculacion anterior");
    }

    // CA-7: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado
        var id = ComputarStreamId(numeroIdentificacion);

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-3 (issue #376): {id} de ruta sin guion -> 400, Identificacion.Parsear como unico punto de
    // traduccion (precedente ObtenerFichaColaborador.FunctionEndpoint).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;
        var idSinGuion = NuevoNumeroIdentificacion(); // p.ej. "3F2A0C..." sin "CC-" adelante

        var response = await AsignarEtiquetaAsync(idSinGuion, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: tipo de identificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) dentro del
    // {id} de ruta -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoTipoDeLaIdentificacionEnLaRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = $"XX-{NuevoNumeroIdentificacion()}";

        var response = await AsignarEtiquetaAsync(id, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400 (Identificacion.Crear rechaza un
    // numero vacio tras la limpieza).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoNumeroDeLaIdentificacionEnLaRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        const string idConNumeroVacio = "CC-";

        var response = await AsignarEtiquetaAsync(idConNumeroVacio, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Body invalido: Valor vacio -> 400 via AsignarEtiquetaBodyValidator.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoValorDelBodyEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = ComputarStreamId(NuevoNumeroIdentificacion());

        var response = await AsignarEtiquetaAsync(id, "Área", valor: "", ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
