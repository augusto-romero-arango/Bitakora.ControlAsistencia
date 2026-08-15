// Issue #349: smoke tests del endpoint POST Colaboradores/Terminaciones (terminar la vinculacion
// vigente de un colaborador). Molde: CrearTurnoSmokeTests (Programacion) -- ambos comandos son
// event-sourcing puro sin consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la unica
// verificacion black-box de los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: TerminarVinculacion exige un ColaboradorAggregateRoot existente con una vinculacion
// abierta -- el arrange de cada test registra el colaborador via POST Colaboradores (el mismo
// comando que lo origina, #330), nunca sembrando datos por fuera del API.
//
// CA-1/CA-2/CA-4 (rutas de exito): 202 + el evento vinculacion_terminada queda persistido en el
// stream "{Tipo}-{Numero}" con la FechaEfectiva exacta del request -- pasada, futura (preaviso, sin
// validacion contra el reloj del servidor en ninguna direccion) o igual a la FechaInicio (vinculacion
// de un solo dia).
// CA-3/CA-4 (rutas de rechazo): el aggregate declina con resultado (nunca lanza, nunca emite un
// evento de fallo persistido) y el handler traduce a 409 -- "ya terminada" (incluye un preaviso cuya
// fecha no ha llegado) y "fecha anterior al inicio" de la vinculacion abierta.
// CA-5: colaborador inexistente -> 404.
// CA-6: request invalida -> 400, sin tocar el event store.
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

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
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

    private static object PayloadTerminacion(
        string numeroIdentificacion, DateOnly fechaEfectiva, string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            fechaEfectiva
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

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectiva), ct);

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

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectivaFutura), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectivaFutura));

        existe.Should().BeTrue(
            $"el preaviso con FechaEfectiva futura ({FormatearFecha(fechaEfectivaFutura)}) deberia persistirse igual que una fecha pasada");
    }

    // CA-4 (rama exitosa): FechaEfectiva == FechaInicio de la vinculacion abierta es una vinculacion
    // de un solo dia, valida.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna202YPersisteFechaEfectiva_CuandoFechaEfectivaEsIgualAFechaInicio()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaUnicoDia = new DateOnly(2026, 7, 4);

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaUnicoDia, ct);

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaUnicoDia), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaUnicoDia));

        existe.Should().BeTrue(
            "una vinculacion de un solo dia (FechaEfectiva == FechaInicio) deberia terminar con exito");
    }

    // CA-3: la ultima vinculacion ya tiene terminacion registrada -> 409, sin importar si la
    // primera terminacion fue un preaviso cuya fecha aun no llego. Simetria con RegistrarColaborador
    // (crear dos veces -> 409); no requiere Postgres, el status code ya prueba que el aggregate
    // declino con resultado y el handler lo tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna409_CuandoVinculacionYaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 10);
        var payloadTerminacion = PayloadTerminacion(numeroIdentificacion, new DateOnly(2026, 2, 1));

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);
        await _client.PostAsJsonAsync(RutaTerminaciones, payloadTerminacion, ct);

        var response = await _client.PostAsJsonAsync(RutaTerminaciones, payloadTerminacion, ct);

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

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, fechaEfectivaAnterior), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await _client.PostAsJsonAsync(
            RutaTerminaciones, PayloadTerminacion(numeroIdentificacion, new DateOnly(2026, 3, 1)), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: FechaEfectiva vacia (default de DateOnly, "no llego" segun la doctrina bitemporal del
    // BC) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoFechaEfectivaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion = NuevoNumeroIdentificacion(),
            fechaEfectiva = default(DateOnly)
        };

        var response = await _client.PostAsJsonAsync(RutaTerminaciones, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadTerminacion(numeroIdentificacion: "", new DateOnly(2026, 3, 1));

        var response = await _client.PostAsJsonAsync(RutaTerminaciones, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadTerminacion(
            NuevoNumeroIdentificacion(), new DateOnly(2026, 3, 1), tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaTerminaciones, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
