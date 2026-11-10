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

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Separado de ComputarStreamId a proposito: que la entrada de la request y el oraculo coincidan es
    // lo que se prueba.
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna204YPersisteFechaEfectiva_CuandoVinculacionEstaAbierta()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 15);
        var fechaEfectiva = new DateOnly(2026, 3, 20);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaEfectiva, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna204YPersisteFechaEfectiva_CuandoFechaEfectivaEsFutura()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaInicio = new DateOnly(2026, 1, 1);
        // Muy en el futuro: ninguna fecha se valida contra el reloj del servidor.
        var fechaEfectivaFutura = new DateOnly(2030, 12, 31);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaInicio, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaEfectivaFutura, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaEfectivaFutura));

        existe.Should().BeTrue(
            $"el preaviso con FechaEfectiva futura ({FormatearFecha(fechaEfectivaFutura)}) deberia persistirse igual que una fecha pasada");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna204YPersisteFechaEfectiva_CuandoFechaEfectivaEsIgualAFechaInicio()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();
        var fechaUnicoDia = new DateOnly(2026, 7, 4);

        var codigo = await RegistrarColaboradorAsync(numeroIdentificacion, fechaUnicoDia, ct);

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, fechaUnicoDia, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var streamId = ComputarStreamId(numeroIdentificacion);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoVinculacionTerminada, Timeout,
            campoJson: "FechaEfectiva", valorJson: FormatearFecha(fechaUnicoDia));

        existe.Should().BeTrue(
            "una vinculacion de un solo dia (FechaEfectiva == FechaInicio) deberia terminar con exito");
    }

    // Un preaviso cuya fecha aun no llego tambien cuenta como terminacion registrada.
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

    // 409 y no 404: es conflicto con el estado vigente (el cliente actua con conocimiento viejo, p. ej.
    // tras un reingreso no visto), no un recurso inexistente.
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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), NuevoCodigoColaborador(), new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await TerminarVinculacionAsync(
            $"XX-{NuevoNumeroIdentificacion()}", NuevoCodigoColaborador(), new DateOnly(2026, 3, 1), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // default(DateOnly) es "no llego" segun la doctrina bitemporal del BC.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna400_CuandoFechaEfectivaEsVacia()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await TerminarVinculacionAsync(
            IdDeRuta(NuevoNumeroIdentificacion()), NuevoCodigoColaborador(), default, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Afirmativo contra el entorno: la ausencia de referencias en la suite no distingue "la ruta se
    // elimino" de "sigue viva y nadie la llama".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task TerminarVinculacion_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        // NumeroIdentificacion vacio a proposito: la ruta vieja, si siguiera viva, responderia 400 de su
        // validador y no un 404 de dominio indistinguible del 404 del host.
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
