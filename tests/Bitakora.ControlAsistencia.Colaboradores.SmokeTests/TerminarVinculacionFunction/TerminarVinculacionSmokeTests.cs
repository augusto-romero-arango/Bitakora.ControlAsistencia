// Issue #379 (MEF-ADR-0043 paso 4, gate empirico de la seccion 8 verificado POSITIVO -- ver
// comentario en FunctionEndpoint.cs y harness#621): smoke tests de POST
// colaboradores/{id}/vinculaciones/{codigo}:terminar (terminar la vinculacion vigente de un
// colaborador, ahora direccionada por su codigo). Reemplaza el POST Colaboradores/Terminaciones
// (issue #349, identificacion en el body): {id} es Identificacion.ToString() ("CC-79543210"),
// parseado UNA sola vez con Identificacion.Parsear (mismo mecanismo que CorregirNombres/
// IniciarVinculacion post-#376/#377/#378). El body se reduce a FechaEfectiva --
// TipoIdentificacion/NumeroIdentificacion ya no viajan alli. Molde: IniciarVinculacionSmokeTests
// (#378) -- mismo comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin
// ServiceBusFixture, la unica verificacion black-box de los efectos del handler es leer mt_events
// via PostgresFixture.
//
// Arrange: TerminarVinculacion exige un ColaboradorAggregateRoot existente con una vinculacion
// abierta -- el arrange de cada test registra el colaborador via POST colaboradores (el mismo
// comando que lo origina, #330), nunca sembrando datos por fuera del API. El codigo vigente de la
// vinculacion inicial es exactamente el CodigoColaborador que RegistrarColaborador recibio
// (ColaboradorAggregateRoot.Registrar reusa VinculacionIniciada(codigo, fechaInicio) -- verificado
// en el aggregate), asi que RegistrarColaboradorAsync devuelve ese codigo para que cada test lo
// use como {codigo} de ruta.
//
// Estos tests dependen de que el deploy publique la ruta nueva en dev: mientras la revision
// anterior siga corriendo, la ruta vieja (POST Colaboradores/Terminaciones) es la unica que existe
// y este archivo -- que solo referencia la ruta nueva -- fallaria por completo (404 del host, no
// el 409/404 de dominio). Mismo precedente que IniciarVinculacionSmokeTests post-#378.
//
// CA-1/CA-2 (rutas de exito): 202 + una VinculacionTerminada persistida en el stream
// "{Tipo}-{Numero}" con la FechaEfectiva exacta del request -- pasada, futura (preaviso, sin
// validacion contra el reloj del servidor en ninguna direccion) o igual a la FechaInicio
// (vinculacion de un solo dia).
// CA-3/CA-4 (rutas de rechazo, reglas conservadas identicas del comando pre-#379): el aggregate
// declina con resultado (nunca lanza, nunca emite un evento de fallo persistido) y el handler
// traduce a 409 -- "ya terminada" (incluye un preaviso cuya fecha no ha llegado) y "fecha anterior
// al inicio" de la vinculacion abierta.
// CA-5: {codigo} de ruta distinto al vigente -> 409 (CodigoNoCorresponde, evaluada PRIMERA por el
// aggregate) -- salvaguarda tipo concurrencia optimista, nunca 404 (es conflicto con el estado
// vigente, no un recurso inexistente).
// CA-6: colaborador inexistente -> 404; {id} de ruta malformado -> 400 via parseo tipado (patron
// #376/#377/#378); FechaEfectiva vacia en el body -> 400.
// CA-7: la ruta vieja Colaboradores/Terminaciones deja de existir -> 404 del host (verificacion
// AFIRMATIVA, mismo criterio que IniciarVinculacionSmokeTests CA-6: el resto de la suite solo
// prueba ausencia de referencias, que no distingue "se elimino" de "sigue viva y nadie la llama").
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.TerminarVinculacionFunction;

public class TerminarVinculacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string RutaTerminacionesVieja = "/api/Colaboradores/Terminaciones";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoVinculacionTerminada = "vinculacion_terminada";
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
    // IniciarVinculacionSmokeTests).
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

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API. Devuelve el codigo de la
    // vinculacion inicial (== CodigoColaborador del comando, verificado en
    // ColaboradorAggregateRoot.Registrar) para que el test lo use como {codigo} de ruta.
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

    private Task<HttpResponseMessage> TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta + FechaEfectiva valida (pasada) ->
    // 202 y el stream recibe vinculacion_terminada con la FechaEfectiva exacta del request. Sin
    // Service Bus (event-sourcing puro): mt_events es la unica ventana black-box a lo que quedo
    // grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna202YPersisteFechaEfectiva_CuandoVinculacionEstaAbierta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);
        var fechaEfectiva = new DateOnly(2026, 3, 20);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaEfectiva, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectiva));

        existe.Should().BeTrue(
            $"el evento {TipoEventoVinculacionTerminada} deberia existir en el stream {streamId} con FechaEfectiva {FormatearFecha(fechaEfectiva)}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectiva), Timeout);

        eventoPersistido.GetProperty("FechaEfectiva").GetString().Should().Be(FormatearFecha(fechaEfectiva));
    }

    // CA-2: FechaEfectiva futura (preaviso) se acepta igual que una pasada -- sin validacion contra
    // el reloj del servidor en ninguna direccion (doctrina bitemporal del BC).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna202YPersisteFechaEfectiva_CuandoFechaEfectivaEsFutura()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 1);
        // Preaviso muy en el futuro -- el punto de esta CA es que NINGUNA fecha, sin importar que
        // tan lejana, se valida contra el reloj del servidor.
        var fechaEfectivaFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaEfectivaFutura, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectivaFutura));

        existe.Should().BeTrue(
            $"el preaviso con FechaEfectiva futura ({FormatearFecha(fechaEfectivaFutura)}) deberia persistirse igual que una fecha pasada");
    }

    // CA-2 (rama exitosa): FechaEfectiva == FechaInicio de la vinculacion abierta es una vinculacion
    // de un solo dia, valida.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna202YPersisteFechaEfectiva_CuandoFechaEfectivaEsIgualAFechaInicio()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaUnicoDia = new DateOnly(2026, 7, 4);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaUnicoDia, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaUnicoDia, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaUnicoDia));

        existe.Should().BeTrue(
            "una vinculacion de un solo dia (FechaEfectiva == FechaInicio) deberia terminar con exito");
    }

    // CA-3: la ultima vinculacion ya tiene terminacion registrada -> 409, sin importar si la
    // primera terminacion fue un preaviso cuya fecha aun no llego. No requiere Postgres: el status
    // code ya prueba que el aggregate declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna409_CuandoVinculacionYaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 10);
        var id = IdDeRuta(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 2, 1), ct);

        var response = await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 2, 5), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4 (rama de rechazo): FechaEfectiva anterior a la FechaInicio de la vinculacion abierta
    // implicaria una duracion negativa -> 409.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna409_CuandoFechaEfectivaEsAnteriorAFechaInicio()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 5, 10);
        var fechaEfectivaAnterior = new DateOnly(2026, 5, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaEfectivaAnterior, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5: {codigo} de ruta distinto al vigente -> 409 (CodigoNoCorresponde), no 404 -- es
    // conflicto con el estado vigente (el cliente actua con conocimiento viejo, ej. tras un
    // reingreso no visto), no un recurso inexistente. La vinculacion sigue abierta y valida: solo
    // el codigo esta mal.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna409_CuandoCodigoDeRutaNoCorrespondeAlVigente()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        var codigoEquivocado = NuevoCodigoColaborador();

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigoEquivocado, new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-6: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: {id} de ruta sin guion -> 400, sin invocar el comando (parseo tipado unico,
    // Identificacion.Parsear).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await TerminarVinculacionAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}",
            NuevoCodigoColaborador(), new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: tipo de identificacion del {id} de ruta fuera de la lista cerrada (PILA: CC, CE, TI,
    // PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await TerminarVinculacionAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: FechaEfectiva vacia en el body (default de DateOnly, "no llego" segun la doctrina
    // bitemporal del BC) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoFechaEfectivaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await TerminarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7 (ruta vieja eliminada): verificado AFIRMATIVAMENTE contra el entorno real -- la ruta
    // vieja (POST Colaboradores/Terminaciones) debe responder 404 del host. El resto de la suite lo
    // cubre solo por ausencia de referencias, que no distingue "la ruta se elimino" de "sigue viva
    // y nadie la llama". Mismo criterio que IniciarVinculacionSmokeTests CA-6.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // NumeroIdentificacion va DELIBERADAMENTE vacio: es lo que vuelve discriminante al oraculo.
        // Con un body valido sobre una identificacion nunca registrada, el endpoint viejo -- si
        // siguiera vivo -- responderia 404 de DOMINIO ("colaborador no encontrado"), indistinguible
        // del 404 del host que este test quiere afirmar. Con NumeroIdentificacion vacio el endpoint
        // viejo corta antes en su IRequestValidator y responde 400 (TerminarVinculacionValidator
        // pre-#379 exigia NumeroIdentificacion no vacio), asi que un 404 aqui solo puede significar
        // que la ruta ya no existe.
        var response = await _client.PostAsJsonAsync(
            RutaTerminacionesVieja,
            new
            {
                tipoIdentificacion = TipoIdentificacionCc,
                numeroIdentificacion = "",
                fechaEfectiva = new DateOnly(2026, 3, 1)
            },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/Terminaciones se reemplazo por POST colaboradores/{id}/vinculaciones/{codigo}:terminar (issue #379): un 400 aqui delataria que la ruta vieja sigue viva");
    }
}
