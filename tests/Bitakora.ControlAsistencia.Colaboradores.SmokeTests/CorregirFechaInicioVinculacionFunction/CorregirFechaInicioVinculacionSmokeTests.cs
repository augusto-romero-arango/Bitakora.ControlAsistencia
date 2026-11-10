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

    // La escritura es sincrona (sin proyeccion downstream): un timeout corto basta para probar la ausencia.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

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

    private async Task TerminarVinculacionAsync(
        string id, string codigo, DateOnly fechaEfectiva, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/colaboradores/{id}/vinculaciones/{codigo}:terminar",
            new { fechaEfectiva },
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que TerminarVinculacion funcione");
    }

    private async Task<string> IniciarVinculacionAsync(string id, DateOnly fechaInicio, CancellationToken ct)
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna204YPersisteFechaInicioVinculacionCorregida_CuandoUltimaVinculacionEstaAbierta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicioOriginal = new DateOnly(2026, 1, 15);
        var fechaCorregida = new DateOnly(2026, 1, 10);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicioOriginal, ct);

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaCorregida, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        var streamId = ComputarStreamId(numeroIdentificacion);

        // El filtro (campoJson, valorJson) ya compara FechaInicio: releer el evento no agrega nada.
        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoFechaInicioVinculacionCorregida, Timeout,
            campoJson: "FechaInicio", valorJson: FormatearFecha(fechaCorregida));

        existe.Should().BeTrue(
            $"el evento {TipoEventoFechaInicioVinculacionCorregida} deberia existir en el stream {streamId} con FechaInicio {FormatearFecha(fechaCorregida)}");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna204YPersisteFechaInicioVinculacionCorregida_CuandoFechaCorregidaEsIgualALaFechaEfectivaPropia()
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

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoFechaInicioVinculacionCorregida, Timeout,
            campoJson: "FechaInicio", valorJson: FormatearFecha(fechaEfectivaTerminacion));

        existe.Should().BeTrue(
            "una FechaCorregida igual a la FechaEfectiva propia deberia aceptarse (vinculacion de un solo dia)");
    }

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

    // El dia de la FechaEfectiva pertenece a la vinculacion que termino (misma frontera que
    // IniciarVinculacion). Solo la vigente es direccionable, de ahi el codigo del reingreso.
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna204SinNuevoEvento_CuandoFechaCorregidaEsIgualALaActual()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 4, 1);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaInicio, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion),
            TipoEventoFechaInicioVinculacionCorregida, TimeoutAusencia);

        existe.Should().BeFalse(
            "una FechaCorregida igual a la actual no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    // El codigo equivocado se evalua ANTES que la idempotencia: un comando a la vinculacion equivocada no
    // debe filtrar el estado de la vigente. Por eso la fecha es la actual, que sola daria 204.
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2026, 5, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirFechaInicioAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2026, 5, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // default(DateOnly) es "no llego" segun la doctrina bitemporal del BC.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna400_CuandoFechaCorregidaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirFechaInicioAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Afirmativo contra el entorno: la ausencia de referencias en la suite no distingue "la ruta se
    // elimino" de "sigue viva y nadie la llama".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirFechaInicioVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // NumeroIdentificacion vacio a proposito: la ruta vieja, si siguiera viva, responderia 400 de su
        // validador y no un 404 de dominio indistinguible del 404 del host.
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
