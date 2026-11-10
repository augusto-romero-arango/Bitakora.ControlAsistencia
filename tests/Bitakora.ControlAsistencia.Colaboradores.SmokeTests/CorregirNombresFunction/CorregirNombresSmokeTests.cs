using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures;
using static Bitakora.ControlAsistencia.Colaboradores.SmokeTests.Fixtures.DatosDePrueba;

namespace Bitakora.ControlAsistencia.Colaboradores.SmokeTests.CorregirNombresFunction;

public class CorregirNombresSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/Colaboradores";
    private const string SchemaColaboradores = "colaboradores";
    private const string TipoEventoNombresCorregidos = "nombres_corregidos";
    private const string TipoIdentificacionCc = "CC";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // La escritura es sincrona (sin proyeccion downstream): un timeout corto basta para probar la ausencia.
    private static readonly TimeSpan TimeoutAusencia = TimeSpan.FromSeconds(3);

    // Segunda lectura de un evento que ExisteEventoAsync ya espero: no hay nada mas que esperar.
    private static readonly TimeSpan TimeoutLecturaConfirmada = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions OpcionesMarten =
        ConfiguracionSerializacionColaboradores.CrearOpcionesMarten();

    // Unico por test: el stream se llama por la identificacion, asi que repetir un numero choca con 409
    // en la segunda corrida. "N" en mayusculas sobrevive a la limpieza del numero y la llave coincide.
    private static string NuevoNumeroIdentificacion() => Guid.CreateVersion7().ToString("N").ToUpperInvariant();

    // Oraculo independiente (MEF-ADR-0002): se recompone a mano, no se deriva de Identificacion.ToString(),
    // para que un cambio de formato en el VO no se auto-valide.
    private static string ComputarStreamId(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    // Separado de ComputarStreamId a proposito: que la entrada de la request y el oraculo coincidan es
    // lo que se prueba, no algo que se pueda asumir compartiendo el metodo.
    private static string IdDeRuta(string numeroIdentificacion) =>
        $"{TipoIdentificacionCc}-{numeroIdentificacion}";

    private static object PayloadRegistro(
        string numeroIdentificacion, DateOnly fechaInicio,
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido,
        string codigoColaborador) => new
        {
            tipoIdentificacion = TipoIdentificacionCc,
            numeroIdentificacion,
            primerNombre,
            segundoNombre,
            primerApellido,
            segundoApellido,
            codigoColaborador,
            fechaInicio
        };

    private static object PayloadCorreccion(
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido) => new
        {
            primerNombre,
            segundoNombre,
            primerApellido,
            segundoApellido
        };

    // Devuelve el codigo de la vinculacion inicial (== CodigoColaborador, ver
    // ColaboradorAggregateRoot.Registrar), que el arrange usa como {codigo} al terminar.
    private async Task<string> RegistrarColaboradorAsync(
        string numeroIdentificacion, DateOnly fechaInicio,
        string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido,
        CancellationToken ct)
    {
        var codigo = NuevoCodigoColaborador();

        var response = await _client.PostAsJsonAsync(
            RutaRegistrar,
            PayloadRegistro(numeroIdentificacion, fechaInicio, primerNombre, segundoNombre, primerApellido, segundoApellido, codigo),
            ct);

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

    private Task<HttpResponseMessage> CorregirNombresAsync(
        string id, string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido,
        CancellationToken ct) =>
        _client.PutAsJsonAsync(
            $"/api/colaboradores/{id}/nombres",
            PayloadCorreccion(primerNombre, segundoNombre, primerApellido, segundoApellido),
            ct);

    // Compara el Nombre por valor (NombreColaborador.Equals) deserializando con las opciones reales de
    // Marten; contra el texto jsonb seria un falso rojo (PostgreSQL no preserva whitespace ni orden de claves).
    private async Task ElStreamRecibioElNombreAsync(string streamId, NombreColaborador nombreEsperado)
    {
        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, streamId, TipoEventoNombresCorregidos, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoNombresCorregidos} deberia existir en el stream {streamId}");

        var eventoPersistido = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaColaboradores, streamId, TipoEventoNombresCorregidos, TimeoutLecturaConfirmada);

        var nombrePersistido = eventoPersistido.GetProperty("Nombre")
            .Deserialize<NombreColaborador>(OpcionesMarten);

        nombrePersistido.Should().Be(nombreEsperado);
    }

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
    public async Task CorregirNombres_Retorna204YPersisteNombresCorregidos_CuandoNombreEsDistintoPorValor()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 15),
            "[TEST]", "Original", "Smoke", null, ct);

        var response = await CorregirNombresAsync(
            IdDeRuta(numeroIdentificacion), "[TEST]", "Corregido", "Smoke", "Segundo", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();

        await ElStreamRecibioElNombreAsync(
            ComputarStreamId(numeroIdentificacion),
            NombreColaborador.Crear("[TEST]", "Corregido", "Smoke", "Segundo"));
    }

    // Los nombres son de la PERSONA, no de la vinculacion: corregir solo exige que el colaborador exista.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna204YPersisteNombresCorregidos_CuandoVinculacionEstaTerminada()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var codigo = await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 2, 1),
            "[TEST]", null, "Terminada", null, ct);
        await TerminarVinculacionAsync(
            IdDeRuta(numeroIdentificacion), codigo, new DateOnly(2026, 3, 1), ct);

        var response = await CorregirNombresAsync(
            IdDeRuta(numeroIdentificacion), "[TEST]", "Reingreso", "Terminada", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await ElStreamRecibioElNombreAsync(
            ComputarStreamId(numeroIdentificacion),
            NombreColaborador.Crear("[TEST]", "Reingreso", "Terminada", null));
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna204SinNuevoEvento_CuandoNombreEsIgualPorValorAlActual()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        await RegistrarColaboradorAsync(
            numeroIdentificacion, new DateOnly(2026, 1, 20),
            "[TEST]", "Igual", "Smoke", null, ct);

        var response = await CorregirNombresAsync(
            IdDeRuta(numeroIdentificacion), "[TEST]", "Igual", "Smoke", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var existe = await postgres.ExisteEventoAsync(
            SchemaColaboradores, ComputarStreamId(numeroIdentificacion), TipoEventoNombresCorregidos,
            TimeoutAusencia);

        existe.Should().BeFalse(
            "un nombre igual por valor al actual no deberia persistir un evento nuevo (idempotencia silenciosa)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna404_CuandoColaboradorNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var numeroIdentificacion = NuevoNumeroIdentificacion();

        var response = await CorregirNombresAsync(
            IdDeRuta(numeroIdentificacion), "[TEST]", null, "Smoke", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Afirmativo contra el entorno: la ausencia de referencias en la suite no distingue "la ruta se
    // elimino" de "sigue viva y nadie la llama". Delata un POST viejo reintroducido "por compatibilidad".
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna404DelHost_CuandoSeLlamaLaRutaViejaPost()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            "/api/Colaboradores/Nombres",
            PayloadCorreccion("[TEST]", null, "Smoke", null),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "POST Colaboradores/Nombres se reemplazo por PUT colaboradores/{id}/nombres (issue #377)");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoIdDeRutaNoTraeGuion()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirNombresAsync(
            $"{TipoIdentificacionCc}{NuevoNumeroIdentificacion()}", "[TEST]", null, "Smoke", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoTipoDeLaIdentificacionDeRutaNoEsReconocido()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirNombresAsync(
            $"XX-{NuevoNumeroIdentificacion()}", "[TEST]", null, "Smoke", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoElNumeroDelIdDeRutaQuedaVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirNombresAsync($"{TipoIdentificacionCc}-", "[TEST]", null, "Smoke", null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoPrimerNombreEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirNombresAsync(
            IdDeRuta(NuevoNumeroIdentificacion()),
            primerNombre: "", segundoNombre: null, primerApellido: "Smoke", segundoApellido: null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task CorregirNombres_Retorna400_CuandoPrimerApellidoEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await CorregirNombresAsync(
            IdDeRuta(NuevoNumeroIdentificacion()),
            primerNombre: "[TEST]", segundoNombre: null, primerApellido: "", segundoApellido: null, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
