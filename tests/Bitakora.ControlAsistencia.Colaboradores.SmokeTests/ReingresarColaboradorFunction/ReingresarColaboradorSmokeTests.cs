// Issue #350: smoke tests del endpoint POST Colaboradores/Reingresos (reingresar a un colaborador
// cuya ultima vinculacion ya fue terminada -- regreso a rol operativo, recontratacion con el mismo
// documento). Molde: TerminarVinculacionSmokeTests (#349) -- mismo comando event-sourcing puro sin
// consumidores downstream (CA-ADR-0030): sin ServiceBusFixture, la unica verificacion black-box de
// los efectos del handler es leer mt_events via PostgresFixture.
//
// Arrange: ReingresarColaborador exige un ColaboradorAggregateRoot existente con la ultima
// vinculacion terminada -- el arrange de cada test registra el colaborador y, cuando aplica,
// termina su vinculacion via los mismos comandos que los originan (#330 y #349), nunca sembrando
// datos por fuera del API.
//
// Evento reutilizado (CA-ADR-0029/MEF-ADR-0039: un evento no conoce su comando): el exito NO crea
// un tipo nuevo -- persiste otra VinculacionIniciada (tipo persistido "vinculacion_iniciada") en el
// stream existente "{Tipo}:{Numero}", con el codigo transaccional nuevo del reingreso.
//
// CA-1/CA-4 (rutas de exito): 202 + una segunda VinculacionIniciada persistida con el Codigo y la
// FechaInicio exactos del request -- ya sea sobre una terminacion pasada (CA-1) o sobre un preaviso
// registrado a futuro (CA-4), sin ninguna validacion contra el reloj del servidor.
// CA-2/CA-3 (rutas de rechazo): el aggregate declina con resultado (nunca lanza, nunca emite un
// evento de fallo persistido) y el handler traduce a 409 -- "vinculacion abierta" (nunca terminada,
// o ya reingresada sin volver a terminar) y "fecha solapa la vinculacion anterior" (FechaInicio
// igual o anterior a la FechaEfectiva de la ultima terminacion, incluido un preaviso no vencido).
// CA-5: colaborador inexistente -> 404.
// CA-6: request invalida -> 400, sin tocar el event store.
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.ReingresarColaboradorFunction;

public class ReingresarColaboradorSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string RutaTerminaciones = "/api/Colaboradores/Terminaciones";
    private const string RutaReingresos = "/api/Colaboradores/Reingresos";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoVinculacionIniciada = "vinculacion_iniciada";
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
        string numeroIdentificacion, string codigoColaborador, DateOnly fechaInicio,
        string tipoIdentificacion = TipoIdentificacionCc) => new
        {
            tipoIdentificacion,
            numeroIdentificacion,
            codigoColaborador,
            fechaInicio
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-1: camino feliz -- ultima vinculacion terminada + FechaInicio estrictamente posterior a la
    // FechaEfectiva -> 202 y el stream recibe otra VinculacionIniciada con el codigo nuevo del
    // reingreso. Sin Service Bus (event-sourcing puro): mt_events es la unica ventana black-box a
    // lo que quedo grabado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna202YPersisteVinculacionIniciada_CuandoFechaInicioEsPosteriorATerminacion()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 15);
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 30);
        var fechaReingreso = new DateOnly(2025, 7, 1);
        var codigoReingreso = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos, PayloadReingreso(numeroIdentificacion, codigoReingreso, fechaReingreso), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoReingreso);

        existe.Should().BeTrue(
            $"el evento {TipoEventoVinculacionIniciada} con Codigo {codigoReingreso} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada,
            campoJson: "Codigo", valorJson: codigoReingreso, Timeout);

        eventoPersistido.GetProperty("FechaInicio").GetString().Should().Be(FormatearFecha(fechaReingreso));
    }

    // CA-4: la ultima terminacion fue un preaviso registrado a futuro y la FechaInicio del reingreso
    // es posterior a ese preaviso -> 202, sin ninguna consulta al reloj del servidor (doctrina
    // bitemporal del BC, en cualquier direccion).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna202YPersisteVinculacionIniciada_CuandoTerminacionFuePreavisoFuturo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2025, 1, 1);
        // Preaviso muy en el futuro -- el punto de esta CA es que NINGUNA fecha, sin importar que
        // tan lejana, se valida contra el reloj del servidor.
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaReingreso = new DateOnly(2031, 1, 1);
        var codigoReingreso = NuevoCodigoColaborador();

        await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaPreaviso, ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos, PayloadReingreso(numeroIdentificacion, codigoReingreso, fechaReingreso), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionIniciada, Timeout,
            campoJson: "Codigo", valorJson: codigoReingreso);

        existe.Should().BeTrue(
            "el reingreso posterior a un preaviso futuro deberia aceptarse sin validar contra el reloj del servidor");
    }

    // CA-2: vinculacion abierta -- nunca hubo una terminacion registrada -> 409. No requiere
    // Postgres: el status code ya prueba que el aggregate declino con resultado y el handler lo
    // tradujo (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna409_CuandoVinculacionNuncaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 3, 1), ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), new DateOnly(2025, 4, 1)),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-2: vinculacion abierta -- un reingreso previo tuvo exito y todavia no se termino -> el
    // segundo reingreso tambien se rechaza (regresion directa del ajuste a Apply(VinculacionIniciada)
    // que reabre la vinculacion: si no reabriera, este segundo reingreso heredaria en falso la
    // terminacion de la vinculacion original y se aceptaria).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna409_CuandoYaFueReingresadoSinTerminarDeNuevo()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 3, 1);
        var fechaPrimerReingreso = new DateOnly(2025, 3, 15);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var primerReingreso = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), fechaPrimerReingreso),
            ct);

        primerReingreso.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el primer reingreso funcione");

        var segundoReingreso = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), new DateOnly(2025, 4, 1)),
            ct);

        segundoReingreso.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: FechaInicio == FechaEfectiva de la ultima terminacion -> 409 -- el mismo dia se rechaza
    // (el dia de la fecha efectiva pertenece a la vinculacion que termina).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna409_CuandoFechaInicioEsIgualAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), fechaEfectivaTerminacion),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: FechaInicio anterior a la FechaEfectiva de la ultima terminacion -> 409 por no-solape.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna409_CuandoFechaInicioEsAnteriorAFechaEfectivaDeTerminacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaTerminacion = new DateOnly(2025, 6, 1);
        var fechaReingresoAnterior = new DateOnly(2025, 5, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaTerminacion, ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), fechaReingresoAnterior),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-3: preaviso no vencido -- la ultima terminacion es un preaviso a futuro y la FechaInicio
    // del reingreso no supera esa fecha futura -> 409 (el preaviso deja "no abierta" la vinculacion,
    // pero la fecha del reingreso sigue exigiendo ser estrictamente posterior a la FechaEfectiva).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna409_CuandoFechaInicioNoSuperaElPreavisoNoVencido()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);
        var fechaReingresoAnteriorAlPreaviso = new DateOnly(2026, 1, 1);

        await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2025, 1, 1), ct);
        await TerminarVinculacionAsync(numeroIdentificacion, fechaEfectivaPreaviso, ct);

        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), fechaReingresoAnteriorAlPreaviso),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // CA-5: colaborador inexistente -> 404, sin escribir nada al event store (no hay stream para
    // consultar: la ausencia de escritura la garantiza el propio 404 -- el handler lanza antes de
    // llegar al aggregate).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion(); // nunca registrado

        var response = await _client.PostAsJsonAsync(
            RutaReingresos,
            PayloadReingreso(numeroIdentificacion, NuevoCodigoColaborador(), new DateOnly(2025, 3, 1)),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // CA-6: CodigoColaborador vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna400_CuandoCodigoColaboradorEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadReingreso(
            NuevoNumeroIdentificacion(), codigoColaborador: "", new DateOnly(2025, 3, 1));

        var response = await _client.PostAsJsonAsync(RutaReingresos, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: FechaInicio vacia (default de DateOnly, "no llego" segun la doctrina bitemporal del BC)
    // -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna400_CuandoFechaInicioEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion = NuevoNumeroIdentificacion(),
            codigoColaborador = NuevoCodigoColaborador(),
            fechaInicio = default(DateOnly)
        };

        var response = await _client.PostAsJsonAsync(RutaReingresos, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: NumeroIdentificacion vacio -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna400_CuandoNumeroIdentificacionEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadReingreso(
            numeroIdentificacion: "", NuevoCodigoColaborador(), new DateOnly(2025, 3, 1));

        var response = await _client.PostAsJsonAsync(RutaReingresos, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-6: TipoIdentificacion fuera de la lista cerrada (PILA: CC, CE, TI, PA, PT) -> 400.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ReingresarColaborador_Retorna400_CuandoTipoIdentificacionNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = PayloadReingreso(
            NuevoNumeroIdentificacion(), NuevoCodigoColaborador(), new DateOnly(2025, 3, 1),
            tipoIdentificacion: "XX");

        var response = await _client.PostAsJsonAsync(RutaReingresos, payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
