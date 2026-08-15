// Issue #352: smoke tests del endpoint POST Colaboradores/FechasInicio (corregir la fecha de
// inicio de la ULTIMA vinculacion de un colaborador, tenga o no terminacion registrada). Quinto
// comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357). Molde:
// CorregirNombresSmokeTests (#351) + IniciarVinculacionSmokeTests (#378) -- mismo comando
// event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la unica
// verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: CorregirFechaInicioVinculacion exige un ColaboradorAggregateRoot existente -- el
// arrange de cada test registra el colaborador y, cuando aplica, termina su vinculacion y/o inicia
// una vinculacion nueva (escenario de reingreso, issue #378) via los mismos comandos que los
// originan (#330, #349, #378), nunca sembrando datos por fuera del API.
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
// CA-5: colaborador inexistente -> 404, sin escribir nada al event store.
// CA-6: request invalida (sin FechaCorregida, sin identificacion, tipo fuera de la lista) -> 400,
// sin tocar el event store.
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
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaFechasInicio = "/api/Colaboradores/FechasInicio";
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

    // Body reducido a los 2 campos que no se derivan de la ruta (issue #378): CodigoColaborador +
    // FechaInicio.
    private static object PayloadIniciarVinculacion(DateOnly fechaInicio) => new
    {
        codigoColaborador = NuevoCodigoColaborador(),
        fechaInicio
    };

    private static object PayloadCorreccion(
        string numeroIdentificacion, DateOnly fechaCorregida, string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            fechaCorregida
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

    // Arrange comun (CA-3): inicia una vinculacion nueva sobre el colaborador tras una terminacion
    // -- escenario de negocio de reingreso -- via el comando que lo origina (issue #378, reemplaza
    // a ReingresarColaborador #350), nunca sembrando el event store por fuera del API.
    private async Task IniciarVinculacionAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{ComputarStreamId(numeroIdentificacion)}/vinculaciones",
            PayloadIniciarVinculacion(fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- colaborador con vinculacion abierta + FechaCorregida distinta valida ->
    // 202 y el stream recibe FechaInicioVinculacionCorregida con la FechaInicio exacta del request.
    // Sin Service Bus (event-sourcing puro): mt_events es la unica ventana black-box a lo que quedo
    // grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna202YPersisteFechaInicioVinculacionCorregida_CuandoUltimaVinculacionEstaAbierta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2026, 1, 15);
        var fechaCorregida = new DateOnly(2026, 1, 10);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio, PayloadCorreccion(numeroIdentificacion, fechaCorregida), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        // El filtro (campoJson, valorJson) de ExisteEventoAsync ya compara el valor persistido de
        // FechaInicio contra el esperado -- releerlo con ObtenerEventoAsync usando el MISMO filtro
        // solo repetiria la consulta para afirmar lo que el filtro ya garantizo. El overload sin
        // filtro sigue siendo necesario en #351 (campo objeto: el Nombre exige comparar por valor
        // deserializado), no aqui: FechaInicio es escalar.
        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoFechaInicioVinculacionCorregida, Timeout,
            campoJson: "FechaInicio", valorJson: FormatearFecha(fechaCorregida));

        existe.Should().BeTrue(
            $"el evento {TipoEventoFechaInicioVinculacionCorregida} deberia existir en el stream {streamId} con FechaInicio {FormatearFecha(fechaCorregida)}");
    }

    // CA-2 (borde valido): la ultima vinculacion esta TERMINADA y FechaCorregida == FechaEfectiva
    // propia -> 202 (vinculacion de un solo dia, consistente con TerminarVinculacion #349).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna202YPersisteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsIgualALaFechaEfectivaPropia()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2026, 2, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio, PayloadCorreccion(numeroIdentificacion, fechaEfectivaTerminacion), ct);

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
        var fechaInicioOriginal = new DateOnly(2026, 2, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio,
            PayloadCorreccion(numeroIdentificacion, fechaEfectivaTerminacion.AddDays(1)),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: tras un reingreso, FechaCorregida IGUAL a la FechaEfectiva de la vinculacion anterior ->
    // 409 por no-solape (el mismo dia se rechaza -- el dia de la fecha efectiva pertenece a la
    // vinculacion que termino, misma frontera que Reingresar #350).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna409_CuandoFechaCorregidaSolapaLaVinculacionAnteriorTrasUnReingreso()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2026, 1, 1);
        var fechaEfectivaTerminacion = new DateOnly(2026, 3, 1);
        var fechaReingreso = new DateOnly(2026, 3, 15);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);
        await IniciarVinculacionAsync(numeroIdentificacion, fechaReingreso, ct);

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio,
            PayloadCorreccion(numeroIdentificacion, fechaEfectivaTerminacion),
            ct);

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

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio, PayloadCorreccion(numeroIdentificacion, fechaInicio), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion),
            TipoEventoFechaInicioVinculacionCorregida, TimeoutAusencia);

        existe.Should().BeFalse(
            "una FechaCorregida igual a la actual no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // CA-5: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await _client.PostAsJsonAsync(
            RutaFechasInicio, PayloadCorreccion(numeroIdentificacion, new DateOnly(2026, 5, 1)), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: FechaCorregida vacia (default de DateOnly, "no llego" segun la doctrina bitemporal del
    // BC) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoFechaCorregidaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion = NuevoNumeroIdentificacion(),
            fechaCorregida = default(DateOnly)
        };

        var response = await _client.PostAsJsonAsync(RutaFechasInicio, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(numeroIdentificacion: "", new DateOnly(2026, 5, 1));

        var response = await _client.PostAsJsonAsync(RutaFechasInicio, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadCorreccion(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 5, 1), tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaFechasInicio, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
