using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.AnularTerminacionFunction;

public class AnularTerminacionSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/colaboradores";
    private const string RutaAnulacionesVieja = "/api/Colaboradores/Terminaciones/Anulaciones";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoTerminacionAnulada = "terminacion_anulada";
    private const string TipoEventoVinculacionTerminada = "vinculacion_terminada";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Separado de ComputarStreamId a proposito: este es la entrada de la request, aquel el oraculo.
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

    private static object PayloadIniciarVinculacion(string codigoColaborador, DateOnly fechaInicio) => new
    {
        codigoColaborador,
        fechaInicio
    };

    // Devuelve el codigo de la vinculacion inicial (== CodigoColaborador, ver
    // ColaboradorAggregateRoot.Registrar), que cada test usa como {codigo} de ruta.
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar, PayloadRegistro(numeroIdentificacion, fechaInicio, codigo), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarColaborador funcione");

        return codigo;
    }

    // Terminar es arrange (TerminarVinculacionAsync, con guardia) y tambien el ACT de la correccion de
    // fecha (PostTerminarAsync, sin guardia: cortar antes taparia su assert semantico).
    private static string RutaTerminar(string id, string codigo) =>
        $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar";

    private Task<HttpResponseMessage> PostTerminarAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct) =>
        _client.PostAsJsonAsync(RutaTerminar(id, codigo), new { fechaEfectiva }, ct);

    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await PostTerminarAsync(id, codigo, fechaEfectiva, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    private async Task<string> IniciarVinculacionAsync(
        string id, DateOnly fechaInicio, CancellationToken ct)
    {
        var codigoNuevo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones",
            PayloadIniciarVinculacion(codigoNuevo, fechaInicio),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que IniciarVinculacion funcione");

        return codigoNuevo;
    }

    private Task<HttpResponseMessage> AnularTerminacionAsync(string id, string codigo, CancellationToken ct) =>
        _client.PostAsync($"/api/colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion", null, ct);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna204YPersisteTerminacionAnulada_CuandoUltimaVinculacionTieneTerminacionRegistrada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 6, 1), ct);

        var response = await AnularTerminacionAsync(id, codigo, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoTerminacionAnulada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoTerminacionAnulada} deberia existir en el stream {streamId}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna204YPersisteTerminacionAnulada_CuandoTerminacionEsUnPreavisoFuturo()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        // Muy en el futuro: la anulacion no consulta el reloj del servidor en ninguna direccion.
        var fechaEfectivaPreaviso = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaPreaviso, ct);

        var response = await AnularTerminacionAsync(id, codigo, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion), TipoEventoTerminacionAnulada, Timeout);

        existe.Should().BeTrue(
            "un preaviso cuya fecha no ha llegado deberia poder anularse igual que una terminacion pasada");
    }

    // Sin endpoint de consulta, que TerminarVinculacion vuelva a aceptar el mismo codigo es la unica
    // prueba black-box de que la anulacion reabrio la vinculacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_ComponeConTerminarVinculacion_ParaCorregirLaFechaDeTerminacion()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var fechaEfectivaErrada = new DateOnly(2026, 6, 1);
        var fechaEfectivaCorregida = new DateOnly(2026, 6, 5);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 15), ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaErrada, ct);

        var anulacion = await AnularTerminacionAsync(id, codigo, ct);
        anulacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que AnularTerminacion funcione");

        var response = await PostTerminarAsync(id, codigo, fechaEfectivaCorregida, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "tras anular, la vinculacion deberia quedar abierta y aceptar una nueva terminacion con el mismo codigo");

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectivaCorregida));

        existe.Should().BeTrue(
            $"el stream {streamId} deberia recibir una segunda vinculacion_terminada con la FechaEfectiva corregida ({FormatearFecha(fechaEfectivaCorregida)})");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoVinculacionNuncaFueTerminada()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);

        var response = await AnularTerminacionAsync(id, codigo, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // Sin idempotencia silenciosa (decision #4 del issue #354): no hay valor que comparar, asi que la
    // segunda anulacion es 409 y no escribe otro terminacion_anulada.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoLaTerminacionYaFueAnuladaAntes()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 3, 1), ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 7, 1), ct);

        var primeraAnulacion = await AnularTerminacionAsync(id, codigo, ct);
        primeraAnulacion.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que la primera anulacion funcione");

        var segundaAnulacion = await AnularTerminacionAsync(id, codigo, ct);

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

    // Tras un reingreso, la terminacion ANTERIOR queda congelada: anularla reabriria una vinculacion
    // teniendo otra abierta (no-solape). Solo la vigente es direccionable, de ahi el codigo del reingreso.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoLaUltimaVinculacionNacioDeUnReingresoSinTerminar()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);
        var fechaEfectivaTerminacionAnterior = new DateOnly(2026, 3, 1);
        var fechaInicioReingreso = new DateOnly(2026, 4, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 1, 1), ct);
        await TerminarVinculacionAsync(id, codigo, fechaEfectivaTerminacionAnterior, ct);
        var codigoReingreso = await IniciarVinculacionAsync(id, fechaInicioReingreso, ct);

        var response = await AnularTerminacionAsync(id, codigoReingreso, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 409 y no 404: es conflicto con el estado vigente (p. ej. un reingreso que el cliente no vio), no
    // un recurso inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna409_CuandoCodigoDeRutaNoCorrespondeAlVigente()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var id = IdDeRuta(numeroIdentificacion);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, new DateOnly(2026, 2, 1), ct);
        await TerminarVinculacionAsync(id, codigo, new DateOnly(2026, 5, 1), ct);

        var response = await AnularTerminacionAsync(id, NuevoCodigoColaborador(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await AnularTerminacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await AnularTerminacionAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await AnularTerminacionAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Afirmativo contra el entorno: la ausencia de referencias en la suite no distingue "la ruta se
    // elimino" de "sigue viva y nadie la llama".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AnularTerminacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // NumeroIdentificacion vacio a proposito: la ruta vieja, si siguiera viva, responderia 400 de su
        // validador y no un 404 de dominio indistinguible del 404 del host.
        var response = await _client.PostAsJsonAsync(
            RutaAnulacionesVieja,
            new { tipoIdentificacion = TipoIdentificacionCc, numeroIdentificacion = "" },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/Terminaciones/Anulaciones se reemplazo por POST colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion (issue #379): un 400 aqui delataria que la ruta vieja sigue viva");
    }
}
