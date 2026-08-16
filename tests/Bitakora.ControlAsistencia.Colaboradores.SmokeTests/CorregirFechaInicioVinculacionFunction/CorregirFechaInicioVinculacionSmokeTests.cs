// Issue #379 (MEF-ADR-0043 paso 4, gate empirico de la seccion 8 verificado POSITIVO -- ver
// comentario en FunctionEndpoint.cs y harness#621): smoke tests de POST
// colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio (corregir la fecha de inicio de
// la ULTIMA vinculacion de un colaborador, tenga o no terminacion registrada, ahora direccionada
// por su codigo). Reemplaza el POST Colaboradores/FechasInicio (issue #352, identificacion en el
// body): {id} es Identificacion.ToString() ("CC-79543210"), parseado UNA sola vez con
// Identificacion.Parsear (mismo mecanismo que TerminarVinculacion/IniciarVinculacion post-#378/
// #379). El body se reduce a FechaCorregida -- TipoIdentificacion/NumeroIdentificacion ya no
// viajan alli. Molde: TerminarVinculacionSmokeTests/AnularTerminacionSmokeTests (#379) -- mismo
// comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la
// unica verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: CorregirFechaInicioVinculacion exige un ColaboradorAggregateRoot existente -- el
// arrange de cada test registra el colaborador y, cuando aplica, termina su vinculacion y/o inicia
// una vinculacion nueva (escenario de reingreso, issue #378) via los mismos comandos que los
// originan (#330, #349, #378, #379), nunca sembrando datos por fuera del API. El codigo vigente de
// la vinculacion inicial es exactamente el CodigoColaborador que RegistrarColaborador recibio
// (ColaboradorAggregateRoot.Registrar reusa VinculacionIniciada(codigo, fechaInicio) -- verificado
// en el aggregate), asi que RegistrarColaboradorAsync devuelve ese codigo para que cada test lo
// use como {codigo} de ruta.
//
// Estos tests dependen de que el deploy publique la ruta nueva en dev: mientras la revision
// anterior siga corriendo, la ruta vieja (POST Colaboradores/FechasInicio) es la unica que existe
// y este archivo -- que solo referencia la ruta nueva -- fallaria por completo (404 del host, no
// el 409/404 de dominio). Mismo precedente que IniciarVinculacionSmokeTests post-#378.
//
// CA-1 (camino feliz, vinculacion abierta): 202 + el stream recibe FechaInicioVinculacionCorregida
// con la FechaInicio exacta del request.
// CA-2 (coherencia interna, ultima vinculacion con terminacion registrada): FechaCorregida ==
// FechaEfectiva propia -> 202 (vinculacion de un solo dia, valida); FechaCorregida > FechaEfectiva
// -> 409 con .resx, sin evento.
// CA-3 (no-solape hacia atras, tras un reingreso): FechaCorregida igual a la FechaEfectiva de la
// vinculacion anterior -> 409, sin evento.
// CA-4 (idempotencia silenciosa): FechaCorregida igual a la fecha de inicio actual -> 202 sin
// evento nuevo (mecanismo "declinar en silencio", precedente CorregirNombres #351).
// CA-5: {codigo} de ruta distinto al vigente -> 409 (CodigoNoCorresponde, evaluada PRIMERA por el
// aggregate, ANTES incluso de la idempotencia SinCambios) -- salvaguarda tipo concurrencia
// optimista, nunca 404.
// CA-6: colaborador inexistente -> 404, sin escribir nada al event store; {id} de ruta malformado
// -> 400; FechaCorregida vacia en el body -> 400.
// CA-7: la ruta vieja Colaboradores/FechasInicio deja de existir -> 404 del host (verificacion
// AFIRMATIVA, mismo criterio que IniciarVinculacionSmokeTests CA-6).
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.CorregirFechaInicioVinculacionFunction;

public class CorregirFechaInicioVinculacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string RutaFechasInicioVieja = "/api/Colaboradores/FechasInicio";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoFechaInicioVinculacionCorregida = "fecha_inicio_vinculacion_corregida";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // CA-4: la ausencia de evento es sincrona (sin proyeccion downstream) -- un timeout corto
    // alcanza para probarla sin alargar la suite esperando los 30s estandar sin ganar senal.
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
    // Separador "-" desde el issue #381.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // El {id} que un cliente real pone en la URL. Deliberadamente separado de ComputarStreamId: uno
    // es la ENTRADA de la request, el otro el ORACULO contra el que se verifica mt_events (mismo
    // criterio que IniciarVinculacionSmokeTests/TerminarVinculacionSmokeTests).
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
    // FechaInicio.
    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
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

    // Arrange comun: cierra la vinculacion vigente -- via el comando que la origina (#349/#379),
    // nunca sembrando el event store por fuera del API.
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

    // Arrange comun (CA-3): inicia una vinculacion nueva sobre el colaborador tras una terminacion
    // -- escenario de negocio de reingreso -- via el comando que lo origina (issue #378), nunca
    // sembrando el event store por fuera del API. Devuelve el codigo de la vinculacion nueva.
    private async Task<string> IniciarVinculacionAsync(string id, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigoNuevo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            PayloadIniciarVinculacion(codigoNuevo, fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");

        return codigoNuevo;
    }

    private Task<HttpResponseMessage> CorregirFechaInicioAsync(
        string id, string codigo, DateOnly fechaCorregida, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio",
            new { fechaCorregida },
            ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta + FechaCorregida distinta valida +
    // {codigo} correcto -> 202 y el stream recibe FechaInicioVinculacionCorregida con la
    // FechaInicio exacta del request. Sin Service Bus (event-sourcing puro): mt_events es la unica
    // ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna202YPersisteFechaInicioVinculacionCorregida_CuandoUltimaVinculacionEstaAbierta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2026, 1, 15);
        var fechaCorregida = new DateOnly(2026, 1, 10);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaCorregida, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        // El filtro (campoJson, valorJson) de ExisteEventoAsync ya compara el valor persistido de
        // FechaInicio contra el esperado -- releerlo con ObtenerEventoAsync usando el MISMO filtro
        // solo repetiria la consulta para afirmar lo que el filtro ya garantizo.
        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoFechaInicioVinculacionCorregida, Timeout,
            campoJson: "FechaInicio", valorJson: FormatearFecha(fechaCorregida));

        existe.Should().BeTrue(
            $"el evento {TipoEventoFechaInicioVinculacionCorregida} deberia existir en el stream {streamId} con FechaInicio {FormatearFecha(fechaCorregida)}");
    }

    // CA-2 (borde valido): la ultima vinculacion esta TERMINADA y FechaCorregida == FechaEfectiva
    // propia -> 202 (vinculacion de un solo dia, consistente con TerminarVinculacion #349/#379).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna202YPersisteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsIgualALaFechaEfectivaPropia()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var fechaInicioOriginal = new DateOnly(2026, 2, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaTerminacion, ct);

        var response = await CorregirFechaInicioAsync(id, codigo, fechaEfectivaTerminacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoFechaInicioVinculacionCorregida, Timeout,
            campoJson: "FechaInicio", valorJson: FormatearFecha(fechaEfectivaTerminacion));

        existe.Should().BeTrue(
            "una FechaCorregida igual a la FechaEfectiva propia deberia aceptarse (vinculacion de un solo dia)");
    }

    // CA-2 (borde invalido): FechaCorregida POSTERIOR a la FechaEfectiva propia de la ultima
    // vinculacion (ya terminada) -> 409. No requiere Postgres: el status code ya prueba que el
    // aggregate declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoFechaCorregidaEsPosteriorALaFechaEfectivaPropia()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var fechaInicioOriginal = new DateOnly(2026, 2, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaTerminacion, ct);

        var response = await CorregirFechaInicioAsync(
            id, codigo, fechaEfectivaTerminacion.AddDays(1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: tras un reingreso, FechaCorregida IGUAL a la FechaEfectiva de la vinculacion anterior ->
    // 409 por no-solape (el mismo dia se rechaza -- el dia de la fecha efectiva pertenece a la
    // vinculacion que termino, misma frontera que IniciarVinculacion #378). Se usa el codigo NUEVO
    // del reingreso -- el {codigo} sigue apuntando a la vinculacion vigente, la unica direccionable.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoFechaCorregidaSolapaLaVinculacionAnteriorTrasUnReingreso()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var fechaInicioOriginal = new DateOnly(2026, 1, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);
        var fechaReingreso = new DateOnly(2026, 3, 15);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaTerminacion, ct);
        var codigoReingreso = await IniciarVinculacionAsync(id, fechaReingreso, ct);

        var response = await CorregirFechaInicioAsync(id, codigoReingreso, fechaEfectivaTerminacion, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4: FechaCorregida igual a la fecha de inicio actual -> 202 sin evento nuevo en el stream
    // (idempotencia silenciosa). Verificacion de ausencia con timeout corto -- ver el porque en el
    // comentario de TimeoutAusencia (arriba).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna202SinNuevoEvento_CuandoFechaCorregidaEsIgualALaActual()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 4, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaInicio, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion),
            TipoEventoFechaInicioVinculacionCorregida, TimeoutAusencia);

        existe.Should().BeFalse(
            "una FechaCorregida igual a la actual no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-5: {codigo} de ruta distinto al vigente -> 409 (CodigoNoCorresponde), evaluada ANTES
    // incluso que la idempotencia (SinCambios) -- un comando dirigido a la vinculacion equivocada no
    // debe filtrar informacion sobre el estado de la vigente, ni siquiera "no habia nada que
    // corregir". Se usa deliberadamente la MISMA fecha de inicio actual (que en solitario
    // declinaria en silencio con 202) para probar que el codigo equivocado gana la evaluacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoCodigoDeRutaNoCorrespondeAlVigente()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 5, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), fechaInicio, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-6: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2026, 5, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: {id} de ruta sin guion -> 400, sin invocar el comando (parseo tipado unico,
    // Identificacion.Parsear).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirFechaInicioAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}",
            NuevoCodigoColaborador(), new DateOnly(2026, 5, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: tipo de identificacion del {id} de ruta fuera de la lista cerrada (PILA: CC, CE, TI,
    // PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirFechaInicioAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2026, 5, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: FechaCorregida vacia en el body (default de DateOnly, "no llego" segun la doctrina
    // bitemporal del BC) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoFechaCorregidaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7 (ruta vieja eliminada): verificado AFIRMATIVAMENTE contra el entorno real -- la ruta
    // vieja (POST Colaboradores/FechasInicio) debe responder 404 del host. El resto de la suite lo
    // cubre solo por ausencia de referencias, que no distingue "la ruta se elimino" de "sigue viva
    // y nadie la llama". Mismo criterio que IniciarVinculacionSmokeTests CA-6.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // NumeroIdentificacion va DELIBERADAMENTE vacio: es lo que vuelve discriminante al oraculo.
        // Con un body valido sobre una identificacion nunca registrada, el endpoint viejo -- si
        // siguiera vivo -- responderia 404 de DOMINIO ("colaborador no encontrado"), indistinguible
        // del 404 del host que este test quiere afirmar. Con NumeroIdentificacion vacio el endpoint
        // viejo corta antes en su IRequestValidator y responde 400
        // (CorregirFechaInicioVinculacionValidator pre-#379 exigia NumeroIdentificacion no vacio),
        // asi que un 404 aqui solo puede significar que la ruta ya no existe.
        var response = await _client.PostAsJsonAsync(
            RutaFechasInicioVieja,
            new
            {
                tipoIdentificacion = TipoIdentificacionCc,
                numeroIdentificacion = "",
                fechaCorregida = new DateOnly(2026, 5, 1)
            },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/FechasInicio se reemplazo por POST colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio (issue #379): un 400 aqui delataria que la ruta vieja sigue viva");
    }
}
