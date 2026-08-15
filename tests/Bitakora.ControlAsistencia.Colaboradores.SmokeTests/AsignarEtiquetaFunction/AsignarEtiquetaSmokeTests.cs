// Issue #355: smoke tests del endpoint POST Colaboradores/Etiquetas (asignar o sobrescribir una
// etiqueta dinamica -- par categoria:valor libre, sin catalogo previo -- a la vinculacion vigente
// de un colaborador). Septimo comando del ciclo de vida de ColaboradorAggregateRoot (desglose
// #348-#357), gemelo de RetirarEtiquetaSmokeTests sobre el mismo diccionario. Molde:
// TerminarVinculacionSmokeTests/IniciarVinculacionSmokeTests/CorregirNombresSmokeTests -- mismo
// comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la
// unica verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: AsignarEtiqueta exige un ColaboradorAggregateRoot existente -- el arrange de cada test
// registra el colaborador y, cuando aplica, termina su vinculacion o inicia una vinculacion nueva
// (escenario de reingreso, issue #378) via los mismos comandos que los originan (#330, #349, #378),
// nunca sembrando datos por fuera del API.
//
// Contenido persistido (Etiqueta, un VO con ctor privado, #353): se verifica deserializando el
// campo "Etiqueta" con la SERIALIZACION REAL de produccion -- Etiqueta +
// ConfiguracionSerializacionColaboradores.CrearOpcionesMarten() (referenciadas desde
// Colaboradores.DomainEvents, ya cableado en el .csproj por el domain-scaffolder). Mismo criterio
// que CorregirNombresSmokeTests con NombreColaborador: "el smoke test deserializa/serializa con el
// tipo que realmente posee el payload persistido". La comparacion es por igualdad de valor
// (Etiqueta.Equals, #353), NUNCA contra el texto JSON persistido (mt_events.data es jsonb; docs
// 8.14.1). Solo es posible en streams que acumulan UN SOLO evento etiqueta_asignada
// (ObtenerEventoAsync sin filtro trae el primero por seq_id -- ver el comentario de
// PostgresFixture.ObtenerEventoAsync sobre campos objeto): cuando un escenario acumula DOS eventos
// de este tipo en el mismo stream (CA-2 sobrescritura, CA-6 reingreso), el efecto se verifica por
// CONTEO (ContarEventosAsync) en vez de contenido -- el detalle exhaustivo de "un valor por
// categoria, nunca duplica" en el diccionario rehidratado ya lo cubre
// AsignarEtiquetaCommandHandlerTests (*.Tests, unit), no se duplica aqui.
//
// CA-1 (ruta de exito): 202 + el stream recibe etiqueta_asignada con la doble forma (original y
// normalizada) de categoria y valor.
// CA-2 (rutas de exito): categoria existente + valor distinto -> sobrescribe (un evento nuevo se
// agrega, conteo pasa de 1 a 2); etiqueta identica por valor -> 202 sin evento nuevo (idempotencia
// silenciosa, conteo se mantiene en 1).
// CA-5 (rutas de rechazo): la ultima vinculacion tiene terminacion registrada -- pasada o un
// preaviso cuya fecha no ha llegado, sin distincion -> 409, sin evento.
// CA-6: tras un reingreso, la vinculacion nueva no hereda las etiquetas de la anterior -- asignar
// sobre la vinculacion vigente crea la categoria desde cero -> 202.
// CA-7: colaborador inexistente -> 404; request invalida (categoria o valor vacios, identificacion
// incompleta, tipo fuera de la lista) -> 400, sin tocar el event store.
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
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaEtiquetas = "/api/Colaboradores/Etiquetas";
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
    // Separador "-" desde el issue #381.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static object PayloadRegistro(string numeroIdentificacion, DateOnly fechaInicio) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        primerNombre = "[TEST]",
        segundoNombre = (string?)null,
        primerApellido = "Smoke",
        segundoApellido = (string?)null,
        codigoColaborador = NuevoCodigoColaborador(),
        fechaInicio
    };

    private static object PayloadTerminacion(string numeroIdentificacion, DateOnly fechaEfectiva) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        fechaEfectiva
    };

    // Body reducido a los 2 campos que no se derivan de la ruta (issue #378): CodigoColaborador +
    // FechaInicio.
    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    private static object PayloadAsignacion(
        string numeroIdentificacion, string categoria, string valor,
        string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            categoria,
            valor
        };

    // Arrange comun: registra un colaborador con una vinculacion abierta -- via el comando que la
    // origina (#330), nunca sembrando el event store por fuera del API.
    private async Task RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");
    }

    // Arrange comun (CA-5): cierra la vinculacion vigente -- via el comando que la origina (#349),
    // nunca sembrando el event store por fuera del API.
    private async Task TerminarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectiva), ct);

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
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct) =>
        _client.PostAsJsonAsync(
            RutaEtiquetas, PayloadAsignacion(numeroIdentificacion, categoria, valor), ct);

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

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);

        var response = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada, TimeoutLecturaConfirmada);

        var etiquetaPersistida = eventoPersistido.GetProperty("Etiqueta").Deserialize<Etiqueta>(OpcionesMarten);

        etiquetaPersistida.Should().Be(Etiqueta.Crear("Área", "Tecnología"));
    }

    // CA-2: asignar sobre una categoria existente (via EsMismaCategoria: "Área" sobre "area") con
    // un valor distinto sobrescribe -- un evento nuevo se agrega (conteo pasa de 1 a 2), a
    // diferencia de la idempotencia silenciosa (ver el siguiente test). El detalle de que el
    // diccionario rehidratado conserve UN SOLO valor por categoria (no dos) ya lo cubre
    // AsignarEtiquetaCommandHandlerTests a nivel unitario -- este smoke test solo verifica el
    // efecto observable black-box: el endpoint acepta la sobrescritura y persiste otro evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202YAgregaOtroEvento_CuandoCategoriaExisteConValorDistinto()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 20), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(numeroIdentificacion, "area", "Medellín", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {streamId}");

        var segundaAsignacion = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Bogotá", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(2,
            "sobrescribir con un valor distinto deberia agregar un evento nuevo (a diferencia de la idempotencia silenciosa)");
    }

    // CA-2: la etiqueta del comando es IGUAL por valor (Etiqueta.Equals, #353) a la ya asignada
    // para esa categoria -> idempotencia silenciosa: ningun evento nuevo, el conteo se mantiene en
    // 1.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna202SinNuevoEvento_CuandoEtiquetaEsIdenticaPorValorALaExistente()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 25), ct);

        var primeraAsignacion = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);
        primeraAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera asignacion funcione");

        var existePrimeraEtiqueta = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada, Timeout);
        existePrimeraEtiqueta.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaAsignada} de la primera asignacion deberia estar en el stream {streamId}");

        // Misma etiqueta por valor, con otra combinacion de mayusculas/tildes en ambos campos.
        var segundaAsignacion = await AsignarEtiquetaAsync(numeroIdentificacion, "area", "tecnologia", ct);

        segundaAsignacion.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada);

        asignaciones.Should().Be(1,
            "una etiqueta identica por valor a la existente no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-5 (decision #1, regla estricta de apertura): la ULTIMA vinculacion tiene terminacion
    // registrada -> 409, sin evento nuevo. No requiere Postgres: el status code ya prueba que el
    // aggregate declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 5, 1), ct);

        var response = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);

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
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaPreavisoFutura, ct);

        var response = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);

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
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);

        var asignacionPrevia = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Ventas", ct);
        asignacionPrevia.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la asignacion previa al reingreso funcione");

        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 6, 1), ct);
        await IniciarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada);

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

        var response = await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Tecnología", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-7: Categoria vacia -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoCategoriaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAsignacion(NuevoNumeroIdentificacion(), categoria: "", valor: "Tecnología");

        var response = await _client.PostAsJsonAsync(RutaEtiquetas, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7: Valor vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoValorEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAsignacion(NuevoNumeroIdentificacion(), categoria: "Área", valor: "");

        var response = await _client.PostAsJsonAsync(RutaEtiquetas, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAsignacion(numeroIdentificacion: "", categoria: "Área", valor: "Tecnología");

        var response = await _client.PostAsJsonAsync(RutaEtiquetas, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarEtiqueta_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAsignacion(
            NuevoNumeroIdentificacion(), categoria: "Área", valor: "Tecnología", tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaEtiquetas, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
