// Issue #354: smoke tests del endpoint POST Colaboradores/Terminaciones/Anulaciones (anular la
// terminacion registrada de la ULTIMA vinculacion de un colaborador). Sexto comando del ciclo de
// vida de ColaboradorAggregateRoot (desglose #348-#357) y el mas simple de la cadena: una sola
// regla, cero fechas en el payload. Molde: TerminarVinculacionSmokeTests/ReingresarColaboradorSmokeTests
// (#349/#350) -- mismo comando event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin
// ServiceBusFixture, la unica verificacion black-box de los efectos del handler es leer mt_events
// via PostgresFixture.
//
// Arrange: AnularTerminacion exige un ColaboradorAggregateRoot existente -- el arrange de cada test
// registra el colaborador y, cuando aplica, termina su vinculacion o lo reingresa via los mismos
// comandos que los originan (#330, #349, #350), nunca sembrando datos por fuera del API.
//
// Evento sin payload (TerminacionAnulada, tipo persistido "terminacion_anulada"): no hay campo de
// contenido que comparar por valor -- cada smoke test usa una identificacion nueva, asi que su
// stream contiene un solo evento de ese tipo y basta con ExisteEventoAsync sin filtro de contenido
// (mismo precedente que CorregirNombresSmokeTests.ElStreamRecibioElNombreAsync).
//
// CA-1 (ruta de exito): 202 + el stream recibe terminacion_anulada. Que la vinculacion reabra con su
// codigo y fecha de inicio ORIGINALES intactos se verifica black-box via composicion (CA-2): sin un
// endpoint de consulta, la unica ventana observable a "quedo abierta otra vez" es que
// TerminarVinculacion (que exige una vinculacion abierta) vuelva a tener exito.
// CA-2: composicion de la correccion -- anular la terminacion errada y volver a terminar con la
// fecha correcta -> 202 + una SEGUNDA VinculacionTerminada persistida con la fecha corregida (las
// reglas de #349 se re-aplican; el flujo completo "corregir fecha de terminacion" funciona en dos
// comandos).
// CA-3 (rutas de rechazo, "vinculacion abierta"): nunca fue terminada, o ya fue anulada antes (dos
// anulaciones seguidas -- no hay idempotencia silenciosa porque no hay valor que comparar, decision
// #4 del issue) -> 409, sin evento nuevo en el stream.
// CA-4: tras un reingreso, la terminacion de la vinculacion ANTERIOR queda CONGELADA -- la ULTIMA
// vinculacion (la del reingreso) es la que cuenta y esta abierta -> 409.
// CA-5: colaborador inexistente -> 404.
// CA-6: request invalida -> 400, sin tocar el event store.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AnularTerminacionFunction;

public class AnularTerminacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaReingresos = "/api/Colaboradores/Reingresos";
    private const string RutaAnulaciones = "/api/Colaboradores/Terminaciones/Anulaciones";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoTerminacionAnulada = "terminacion_anulada";
    private const string TipoEventoVinculacionTerminada = "vinculacion_terminada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Numero unico por test -- evita colisiones entre ejecuciones repetidas del smoke test: la
    // identidad del stream es Identificacion.ToString() ("CC:<numero>"), no un Guid nuevo por
    // llamada, asi que reusar un numero fijo haria que el arrange (RegistrarColaborador) choque con
    // 409 en la segunda corrida.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    private static string NuevoCodigoColaborador() => $"[TEST]-{Guid.CreateVersion7()}";

    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}:{numeroIdentificacion}";

    private static string FormatearFecha(DateOnly fecha) =>
        fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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

    private static object PayloadAnulacion(
        string numeroIdentificacion, string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion
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

    // Arrange comun: cierra la vinculacion vigente -- via el comando que la origina (#349), nunca
    // sembrando el event store por fuera del API.
    private async Task TerminarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectiva), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    // Arrange comun (CA-4): reingresa al colaborador tras una terminacion -- via el comando que lo
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- la ultima vinculacion tiene terminacion registrada + POST valido -> 202
    // y el stream recibe terminacion_anulada. Sin Service Bus (event-sourcing puro): mt_events es
    // la unica ventana black-box a lo que quedo grabado. El evento no tiene payload -- no hay
    // contenido que comparar por valor, solo su existencia en el stream.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna202YPersisteTerminacionAnulada_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 6, 1), ct);

        var response = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoTerminacionAnulada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoTerminacionAnulada} deberia existir en el stream {streamId}");
    }

    // CA-1: la anulacion tambien procede sobre un preaviso cuya fecha aun no llego -- sin reloj, el
    // dato estaba mal y el sistema registra cuando se entero, sin importar si la fecha del preaviso
    // ya paso o no.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna202YPersisteTerminacionAnulada_CuandoTerminacionEsUnPreavisoFuturo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        // Preaviso muy en el futuro -- el punto de esta CA es que la anulacion no consulta el reloj
        // del servidor en ninguna direccion.
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaPreaviso, ct);

        var response = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion), TipoEventoTerminacionAnulada, Timeout);

        existe.Should().BeTrue(
            "un preaviso cuya fecha no ha llegado deberia poder anularse igual que una terminacion pasada");
    }

    // CA-2: composicion de la correccion de una fecha de terminacion errada -- anular la terminacion
    // errada y volver a terminar con la fecha correcta -> 202 y una SEGUNDA VinculacionTerminada
    // persistida con la fecha corregida. La reapertura de la vinculacion (que TerminarVinculacion
    // exige) es la unica ventana black-box observable a que la anulacion tuvo el efecto esperado --
    // sin un endpoint de consulta, no hay otra forma de verificarlo desde afuera.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_ComponeConTerminarVinculacion_ParaCorregirLaFechaDeTerminacion()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaErrada = new DateOnly(2026, 6, 1);
        var fechaEfectivaCorregida = new DateOnly(2026, 6, 5);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaErrada, ct);

        var anulacion = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);
        anulacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que AnularTerminacion funcione");

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectivaCorregida), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "tras anular, la vinculacion deberia quedar abierta y aceptar una nueva terminacion");

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectivaCorregida));

        existe.Should().BeTrue(
            $"el stream {streamId} deberia recibir una segunda vinculacion_terminada con la FechaEfectiva corregida ({FormatearFecha(fechaEfectivaCorregida)})");
    }

    // CA-3: la vinculacion vigente nunca fue terminada -> 409. No requiere Postgres: el status code
    // ya prueba que el aggregate declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoVinculacionNuncaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3 (decision #4 del issue): anular dos veces -- la segunda anulacion encuentra la
    // vinculacion ya abierta (no hay idempotencia silenciosa porque no hay valor que comparar) ->
    // 409, sin escribir un segundo terminacion_anulada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoLaTerminacionYaFueAnuladaAntes()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 3, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, new DateOnly(2026, 7, 1), ct);

        var primeraAnulacion = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);
        primeraAnulacion.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que la primera anulacion funcione");

        var segundaAnulacion = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        segundaAnulacion.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoTerminacionAnulada, Timeout);

        existe.Should().BeTrue(
            $"el terminacion_anulada de la primera anulacion deberia estar en el stream {streamId}");

        var anulaciones = await postgres.ContarEventosAsync(
            SchemaColaboradores, streamId, TipoEventoTerminacionAnulada);

        anulaciones.Should().Be(1,
            "la segunda anulacion se rechazo con 409: no debe haber escrito un segundo terminacion_anulada");
    }

    // CA-4 (decision #3 del issue, aprobada explicitamente): tras un reingreso, la terminacion de la
    // vinculacion ANTERIOR queda CONGELADA -- la ULTIMA vinculacion (la del reingreso) es la que
    // cuenta y esta abierta -> 409. Anularla reabriria una vinculacion teniendo otra abierta, lo que
    // la invariante de no-solape prohibe.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoLaUltimaVinculacionNacioDeUnReingresoSinTerminar()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacionAnterior, ct);
        await ReingresarColaboradorAsync(numeroIdentificacion, fechaInicioReingreso, ct);

        var response = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await _client.PostAsJsonAsync(
            RutaAnulaciones, PayloadAnulacion(numeroIdentificacion), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAnulacion(numeroIdentificacion: "");

        var response = await _client.PostAsJsonAsync(RutaAnulaciones, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadAnulacion(NuevoNumeroIdentificacion(), tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaAnulaciones, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
