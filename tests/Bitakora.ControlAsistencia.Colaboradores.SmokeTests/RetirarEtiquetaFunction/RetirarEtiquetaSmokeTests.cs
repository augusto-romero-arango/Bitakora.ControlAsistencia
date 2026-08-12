// Issue #355: smoke tests del endpoint POST Colaboradores/Etiquetas/Retiros (retirar una etiqueta
// dinamica de la vinculacion vigente de un colaborador). Octavo comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357), gemelo de AsignarEtiquetaSmokeTests sobre el mismo
// diccionario. Molde: TerminarVinculacionSmokeTests/ReingresarColaboradorSmokeTests -- mismo
// comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la
// unica verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: RetirarEtiqueta exige un ColaboradorAggregateRoot existente con la categoria YA
// ASIGNADA -- el arrange de cada test registra el colaborador y asigna (y, cuando aplica, termina
// su vinculacion o lo reingresa) via los mismos comandos que los originan (#330, #349, #350, y
// AsignarEtiqueta del propio #355), nunca sembrando datos por fuera del API.
//
// Contenido persistido (EtiquetaRetirada, payload plano con solo CategoriaNormalizada -- un campo
// ESCALAR top-level, a diferencia de EtiquetaAsignada): a diferencia de AsignarEtiquetaSmokeTests,
// aqui SI se puede filtrar por (campoJson, valorJson) con el overload estandar de
// PostgresFixture.ExisteEventoAsync/ObtenerEventoAsync, incluso en streams que acumulan mas de un
// evento etiqueta_retirada.
//
// CA-3 (ruta de exito): 202 + el stream recibe etiqueta_retirada con la categoria normalizada,
// retirando por una forma distinta a la asignada ("área" retira lo asignado como "Area").
// CA-4 (rutas de rechazo, decision #2 -- SIN idempotencia silenciosa): categoria nunca asignada, o
// un error de transcripcion sobre una categoria existente ("Aera" vs "Area") -> 409, sin evento
// nuevo, la etiqueta existente (si la hay) queda intacta.
// CA-5 (rutas de rechazo): la ultima vinculacion tiene terminacion registrada -- pasada o un
// preaviso cuya fecha no ha llegado, sin distincion -> 409, sin evento.
// CA-6: la etiqueta pertenecia a la vinculacion ANTERIOR (congelada tras la terminacion) -- la
// vinculacion vigente (el reingreso) no la hereda, asi que retirarla encuentra la categoria
// inexistente -> 409, igual que cualquier categoria nunca asignada.
// CA-7: colaborador inexistente -> 404; request invalida (categoria vacia, identificacion
// incompleta, tipo fuera de la lista) -> 400, sin tocar el event store.
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.RetirarEtiquetaFunction;

public class RetirarEtiquetaSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaReingresos = "/api/Colaboradores/Reingresos";
    private const string RutaEtiquetas = "/api/Colaboradores/Etiquetas";
    private const string RutaRetiros = "/api/Colaboradores/Etiquetas/Retiros";
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
    // identidad del stream es Identificacion.ToString() ("CC:<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    private static string NuevoCodigoColaborador() => $"[TEST]-{Guid.CreateVersion7()}";

    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}:{numeroIdentificacion}";

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

    private static object PayloadReingreso(
        string numeroIdentificacion, string codigoColaborador, DateOnly fechaInicio) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            codigoColaborador,
            fechaInicio
        };

    private static object PayloadAsignacion(string numeroIdentificacion, string categoria, string valor) => new
    {
        tipoIdentificacion = TipoIdentificacionCc,
        numeroIdentificacion,
        categoria,
        valor
    };

    private static object PayloadRetiro(
        string numeroIdentificacion, string categoria, string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            categoria
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

    // Arrange comun: asigna la etiqueta que luego se intenta retirar -- via el comando que la
    // origina (AsignarEtiqueta, propio issue #355), nunca sembrando el event store por fuera del
    // API.
    private async Task AsignarEtiquetaAsync(
        string numeroIdentificacion, string categoria, string valor, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaEtiquetas, PayloadAsignacion(numeroIdentificacion, categoria, valor), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AsignarEtiqueta funcione");
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

    // Arrange comun (CA-6): reingresa al colaborador tras una terminacion -- via el comando que lo
    // origina (#350), nunca sembrando el event store por fuera del API.
    private async Task ReingresarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que ReingresarColaborador funcione");
    }

    private Task<HttpResponseMessage> RetirarEtiquetaAsync(
        string numeroIdentificacion, string categoria, CancellationToken ct) =>
        _client.PostAsJsonAsync(RutaRetiros, PayloadRetiro(numeroIdentificacion, categoria), ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-3: camino feliz -- retirar por una forma distinta de la que se asigno ("área" retira lo
    // asignado como "Area", misma categoria normalizada) -> 202 y el stream recibe
    // etiqueta_retirada con la categoria normalizada. Sin Service Bus (event-sourcing puro):
    // mt_events es la unica ventana black-box a lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna202YPersisteEtiquetaRetirada_CuandoCategoriaExisteConFormaDistinta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaRetirada, Timeout,
            campoJson: "CategoriaNormalizada", valorJson: "area");

        existe.Should().BeTrue(
            $"el evento {TipoEventoEtiquetaRetirada} con CategoriaNormalizada 'area' deberia existir en el stream {streamId}");
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

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-4 (el typo debe aflorar, decision #2 del issue): "Aera" no es "Area" -- categorias
    // distintas normalizadas, aunque exista una etiqueta para "Area" -> 409 igual, ninguna
    // etiqueta_retirada nueva; la etiqueta existente ("Area") queda intacta (el conteo de
    // etiqueta_asignada se mantiene en 1).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoHayUnErrorDeTranscripcionEnLaCategoria()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var streamId = ComputarStreamId(numeroIdentificacion);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 5), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Area", "Ventas", ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Aera", ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var existeRetiro = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaRetirada, TimeoutAusencia);
        existeRetiro.Should().BeFalse(
            "un error de transcripcion en la categoria no deberia persistir un etiqueta_retirada");

        var asignaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoEtiquetaAsignada);
        asignaciones.Should().Be(1,
            "la etiqueta original ('Area') deberia quedar intacta -- el rechazo no la toca");
    }

    // CA-5 (decision #1, regla estricta de apertura): la ULTIMA vinculacion tiene terminacion
    // registrada -> 409, sin evento nuevo.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna409_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 10), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 6, 1), ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Área", ct);

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
        var fechaPreavisoFutura = new DateOnly(2030, 12, 31);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaPreavisoFutura, ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Área", ct);

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

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 10), ct);
        await AsignarEtiquetaAsync(numeroIdentificacion, "Área", "Ventas", ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 6, 1), ct);
        await ReingresarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Área", ct);

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

        var response = await RetirarEtiquetaAsync(numeroIdentificacion, "Área", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-7: Categoria vacia -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoCategoriaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRetiro(NuevoNumeroIdentificacion(), categoria: "");

        var response = await _client.PostAsJsonAsync(RutaRetiros, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRetiro(numeroIdentificacion: "", categoria: "Área");

        var response = await _client.PostAsJsonAsync(RutaRetiros, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-7: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RetirarEtiqueta_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadRetiro(NuevoNumeroIdentificacion(), categoria: "Área", tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaRetiros, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
