// Issue #378 (MEF-ADR-0043 paso 1): smoke tests de POST colaboradores/{id}/vinculaciones (iniciar
// una vinculacion nueva sobre un colaborador EXISTENTE -- create disfrazado, el mismo evento que
// abre la vinculacion en RegistrarColaborador). Reemplaza el POST Colaboradores/Reingresos (issue
// #350, identificacion en el body): {id} es Identificacion.ToString() ("CC-79543210"), parseado UNA
// sola vez con Identificacion.Parsear (mismo mecanismo que CorregirNombres/AsignarEtiqueta
// post-#376/#377). El body se reduce a CodigoColaborador + FechaInicio -- TipoIdentificacion/
// NumeroIdentificacion ya no viajan alli. Molde: CorregirNombresSmokeTests /
// ReingresarColaboradorSmokeTests (este archivo la reemplaza y se elimina, issue #378) -- mismo
// comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la
// unica verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: IniciarVinculacion exige un ColaboradorAggregateRoot existente con la ultima vinculacion
// terminada -- el arrange de cada test registra el colaborador (via /api/colaboradores, kebab-case
// desde este mismo issue, CA-5) y, cuando aplica, termina su vinculacion via el comando que la
// origina (#349/#379), nunca sembrando datos por fuera del API. Issue #379: la terminacion ahora
// exige el {codigo} de la vinculacion en la ruta -- RegistrarColaboradorAsync devuelve el codigo
// (== CodigoColaborador del comando, verificado en ColaboradorAggregateRoot.Registrar) para que el
// arrange lo use como {codigo} al terminar.
//
// Evento reutilizado (CA-ADR-0029/MEF-ADR-0039: un evento no conoce su comando): el exito NO crea
// un tipo nuevo -- persiste otra VinculacionIniciada (tipo persistido "vinculacion_iniciada") en el
// stream existente "{Tipo}-{Numero}", con el codigo transaccional nuevo de la vinculacion.
//
// Estos tests dependen de que el deploy publique la ruta POST colaboradores/{id}/vinculaciones en
// dev: mientras la revision anterior siga corriendo, la ruta vieja (POST Colaboradores/Reingresos)
// es la unica que existe y este archivo -- que solo referencia la ruta nueva -- fallaria por
// completo (404 del host, no el 404 de dominio de CA-3). Mismo precedente que
// CorregirNombresSmokeTests post-#377.
//
// CA-1 (rutas de exito): 202 + una segunda VinculacionIniciada persistida con el Codigo y la
// FechaInicio exactos del request -- ya sea sobre una terminacion pasada o sobre un preaviso
// registrado a futuro, sin ninguna validacion contra el reloj del servidor.
// CA-2 (rutas de rechazo, reglas conservadas identicas del comando absorbido): el aggregate declina
// con resultado (nunca lanza, nunca emite un evento de fallo persistido) y el handler traduce a
// 409 -- "vinculacion abierta" (nunca terminada, o ya iniciada sin volver a terminar) y "fecha
// solapa la vinculacion anterior" (FechaInicio igual o anterior a la FechaEfectiva de la ultima
// terminacion, incluido un preaviso no vencido).
// CA-3: colaborador inexistente -> 404; {id} de ruta malformado -> 400 via parseo tipado (patron
// #376/#377).
// CA-4 (comando/Function/folder/handler/validator/enum/metodo renombrados a "iniciar vinculacion"):
// no es observable black-box -- lo cubre la lectura del codigo fuente, no un smoke test.
// CA-6: la ruta vieja Colaboradores/Reingresos deja de existir -> 404 del host (verificacion
// AFIRMATIVA, mismo criterio que CorregirNombresSmokeTests CA-5: el resto de la suite solo prueba
// ausencia de referencias, que no distingue "se elimino" de "sigue viva y nadie la llama").
//
// Issue #387 (CodigoColaborador URL-safe, invariante heredada sin cambios): CA-1 con caracteres
// unreserved no alfanumericos (. _ ~) -> 202; CA-2/CA-3 con ":" (separador de accion reservado,
// MEF-ADR-0043) y espacio (fuera del set unreserved RFC 3986) -> 400.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.IniciarVinculacionFunction;

public class IniciarVinculacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string RutaReingresosVieja = "/api/Colaboradores/Reingresos";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoVinculacionIniciada = "vinculacion_iniciada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC-<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida. El formato "N" en MAYUSCULAS ya es alfanumerico ASCII, asi que
    // sobrevive intacto a la limpieza del numero (#381) y la llave esperada de abajo coincide con
    // la que arma el backend.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): se recompone aqui a mano, no se
    // deriva de Identificacion.ToString(), para que un cambio de formato en el VO no se auto-valide.
    // Separador "-" desde el issue #381.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // El {id} que un cliente real pone en la URL. Deliberadamente separado de ComputarStreamId: uno
    // es la ENTRADA de la request, el otro el ORACULO contra el que se verifica mt_events -- que
    // hoy coincidan textualmente es justamente lo que estos tests prueban (mismo criterio que
    // CorregirNombresSmokeTests).
    private static string IdDeRuta(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static string FormatearFecha(DateOnly fecha) =>
        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
    // FechaInicio -- TipoIdentificacion/NumeroIdentificacion ya no viajan aqui, se derivan de {id}.
    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

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

    // Arrange comun: cierra la vinculacion vigente -- via el comando que la origina (#349/#379),
    // nunca sembrando el event store por fuera del API. Issue #379: la ruta gano el {codigo} -- ya
    // no es "/api/Colaboradores/Terminaciones" con identificacion en el body.
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

    private Task<HttpResponseMessage> IniciarVinculacionAsync(
        string id, string codigoColaborador, DateOnly fechaInicio, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            PayloadIniciarVinculacion(codigoColaborador, fechaInicio),
            ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- ultima vinculacion terminada + FechaInicio estrictamente posterior a la
    // FechaEfectiva -> 202 y el stream recibe otra VinculacionIniciada con el codigo nuevo. Sin
    // Service Bus (event-sourcing puro): mt_events es la unica ventana black-box a lo que quedo
    // grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna202YPersisteVinculacionIniciada_CuandoFechaInicioEsPosteriorATerminacion()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 15);
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 30);
        var fechaNuevaVinculacion = new DateOnly(2025, 7, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            $"el evento {TipoEventoVinculacionIniciada} con Codigo {codigoNuevo} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada,
            campoJson: "Codigo", valorJson: codigoNuevo, Timeout);

        eventoPersistido.GetProperty("FechaInicio").GetString().Should().Be(FormatearFecha(fechaNuevaVinculacion));
    }

    // CA-1: la ultima terminacion fue un preaviso registrado a futuro y la FechaInicio de la nueva
    // vinculacion es posterior a ese preaviso -> 202, sin ninguna consulta al reloj del servidor
    // (doctrina bitemporal del BC, en cualquier direccion).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna202YPersisteVinculacionIniciada_CuandoTerminacionFuePreavisoFuturo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 1);
        // Preaviso muy en el futuro -- el punto de esta CA es que NINGUNA fecha, sin importar que
        // tan lejana, se valida contra el reloj del servidor.
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaNuevaVinculacion = new DateOnly(2031, 1, 1);
        var codigoNuevo = NuevoCodigoColaborador();

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaPreaviso, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            "la nueva vinculacion posterior a un preaviso futuro deberia aceptarse sin validar contra el reloj del servidor");
    }

    // CA-2: vinculacion abierta -- nunca hubo una terminacion registrada -> 409. No requiere
    // Postgres: el status code ya prueba que el aggregate declino con resultado y el handler lo
    // tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoVinculacionNuncaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 3, 1), ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 4, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: vinculacion abierta -- una vinculacion nueva ya fue iniciada con exito y todavia no se
    // termino -> el segundo intento tambien se rechaza (regresion directa del ajuste a
    // Apply(VinculacionIniciada) que reabre la vinculacion: si no reabriera, este segundo intento
    // heredaria en falso la terminacion de la vinculacion original y se aceptaria).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoYaFueIniciadaSinTerminarDeNuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 3, 1);
        var fechaPrimeraVinculacionNueva = new DateOnly(2025, 3, 15);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var primeraVinculacionNueva = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaPrimeraVinculacionNueva, ct);

        primeraVinculacionNueva.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera vinculacion nueva funcione");

        var segundaVinculacionNueva = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 4, 1), ct);

        segundaVinculacionNueva.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: FechaInicio == FechaEfectiva de la ultima terminacion -> 409 -- el mismo dia se rechaza
    // (el dia de la fecha efectiva pertenece a la vinculacion que termina).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioEsIgualAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaEfectivaTerminacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: FechaInicio anterior a la FechaEfectiva de la ultima terminacion -> 409 por no-solape.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioEsAnteriorAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);
        var fechaAnterior = new DateOnly(2025, 5, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaAnterior, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: preaviso no vencido -- la ultima terminacion es un preaviso a futuro y la FechaInicio de
    // la nueva vinculacion no supera esa fecha futura -> 409 (el preaviso deja "no abierta" la
    // vinculacion, pero la fecha sigue exigiendo ser estrictamente posterior a la FechaEfectiva).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna409_CuandoFechaInicioNoSuperaElPreavisoNoVencido()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaAnteriorAlPreaviso = new DateOnly(2026, 1, 1);

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaPreaviso, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaAnteriorAlPreaviso, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-3: {id} de ruta sin guion -> 400, sin invocar el comando (parseo tipado unico,
    // Identificacion.Parsear).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}",
            NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: tipo de identificacion del {id} de ruta fuera de la lista cerrada (PILA: CC, CE, TI,
    // PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3: numero vacio tras el guion del {id} de ruta -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoElNumeroDelIdDeRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            $"{TipoIdentificacionCc}-", NuevoCodigoColaborador(), new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6 (body reducido): CodigoColaborador vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador: "", new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: FechaInicio vacia (default de DateOnly, "no llego" segun la doctrina bitemporal del BC)
    // -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoFechaInicioEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default(DateOnly), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6 (ruta vieja eliminada): verificado AFIRMATIVAMENTE contra el entorno real -- la ruta
    // vieja (POST Colaboradores/Reingresos) debe responder 404 del host. El resto de la suite lo
    // cubre solo por ausencia de referencias, que no distingue "la ruta se elimino" de "sigue viva y
    // nadie la llama". Toma la forma de CorregirNombresSmokeTests CA-5, pero corrige su oraculo:
    // ver el comentario del body de abajo (ese precedente afirma el 404 con un body VALIDO, que el
    // endpoint viejo tambien responderia como 404 de dominio -- defecto propio de #377, no replicado
    // aqui).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // El body va DELIBERADAMENTE invalido (codigoColaborador vacio): es lo que vuelve
        // discriminante al oraculo. Con un body valido sobre una identificacion nunca registrada,
        // el endpoint viejo -- si siguiera vivo -- responderia 404 de DOMINIO ("colaborador no
        // encontrado"), indistinguible del 404 del host que este test quiere afirmar: el test
        // pasaria en verde con la ruta vieja intacta, justo el falso positivo que dice prevenir.
        // Con el body invalido el endpoint viejo corta antes en su IRequestValidator y responde 400
        // (comprobado contra dev con la revision anterior desplegada), asi que un 404 aqui solo
        // puede significar que la ruta ya no existe.
        var response = await _client.PostAsJsonAsync(
            RutaReingresosVieja,
            new
            {
                tipoIdentificacion = TipoIdentificacionCc,
                numeroIdentificacion = NuevoNumeroIdentificacion(),
                codigoColaborador = "",
                fechaInicio = new DateOnly(2025, 3, 1)
            },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/Reingresos se reemplazo por POST colaboradores/{id}/vinculaciones (issue #378): un 400 aqui delataria que la ruta vieja sigue viva");
    }

    // CA-1 (#387, invariante heredada): codigo con caracteres unreserved no alfanumericos (. _ ~)
    // tambien produce 202 -- el set permitido no se limita a alfanumerico+guion, que es lo unico
    // que ejercita el helper compartido NuevoCodigoColaborador ("TEST-<guid>"). Verificacion
    // end-to-end de que el regex desplegado en dev no es mas restrictivo que el unreserved de RFC
    // 3986 seccion 2.3.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna202_CuandoCodigoColaboradorTieneCaracteresUnreservedNoAlfanumericos()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 30);
        var fechaNuevaVinculacion = new DateOnly(2025, 7, 1);
        var codigoNuevo = $"a.b_{Guid.CreateVersion7()}~2";

        var codigoVigente = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 15), ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoVigente, fechaEfectivaTerminacion, ct);

        var response = await IniciarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoNuevo, fechaNuevaVinculacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoNuevo);

        existe.Should().BeTrue(
            $"el codigo con caracteres unreserved no alfanumericos deberia haberse aceptado y persistido en {streamId}");
    }

    // CA-2 (#387, invariante heredada): ":" esta explicitamente fuera del set permitido --
    // MEF-ADR-0043 seccion 1 lo reserva como separador de accion (vinculaciones/{codigo}:terminar,
    // #379). Un codigo con ":" haria inparseable esa ruta -- caso destacado del issue.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorContieneDosPuntos()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL:{Guid.CreateVersion7()}";

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador, new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-3 (#387, invariante heredada): cualquier otro caracter fuera del set (espacio, aqui) ->
    // 400. La exhaustividad del regex (acento, "/") ya la cubre IniciarVinculacionBodyValidatorTests;
    // este smoke test solo confirma que la regla llega desplegada end-to-end en dev.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task IniciarVinculacion_Retorna400_CuandoCodigoColaboradorContieneEspacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoColaborador = $"COL {Guid.CreateVersion7()}";

        var response = await IniciarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), codigoColaborador, new DateOnly(2025, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
