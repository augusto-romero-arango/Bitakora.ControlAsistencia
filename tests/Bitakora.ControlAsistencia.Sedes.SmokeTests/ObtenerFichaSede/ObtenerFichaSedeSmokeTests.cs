// Arrange via API, nunca sembrando el event store por fuera de ella. FichaSede se materializa de
// forma asincrona (MEF-ADR-0034 seccion 3): el camino feliz envuelve la consulta en
// Polling.WaitUntilAsync, unica excepcion documentada al "no usar Polling directo en tests".
//
// Las reglas de cada evento que alimenta la proyeccion las cubre el unit test de
// FichaSedeProjection: aqui solo el shape basico de la vista y los bordes 404/400.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ObtenerFichaSede;

public class ObtenerFichaSedeSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Case-insensitive: la respuesta viaja en camelCase (ComposicionServicios configura
    // JsonNamingPolicy.CamelCase), mientras que las formas locales de este archivo son PascalCase.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Forma local desacoplada de ReadModels.Sedes.FichaSede: el smoke test no referencia ReadModels
    // ni el Function App (MEF-ADR-0034 seccion 5).
    private sealed record FichaSedeRespuestaSmoke(
        string Id,
        string Codigo,
        string Nombre,
        string? Ciudad,
        string? Direccion,
        string? CentroDeCostos,
        bool Activa,
        IReadOnlyList<string> Dispositivos);

    // "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y "[", "]" y el espacio no son URL-safe.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string Ruta(string codigo) => $"/api/sedes/fichas/{codigo}";

    private static object PayloadRegistro(string codigo, string nombre) => new
    {
        codigo,
        nombre,
        ciudad = (string?)null,
        direccion = (string?)null
    };

    private async Task RegistrarSedeAsync(string codigo, string nombre, CancellationToken ct)
    {
        var response = await _client.PostAsJsonAsync(RutaRegistrar, PayloadRegistro(codigo, nombre), ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que RegistrarSede funcione");
    }

    // Reintenta el GET hasta que la proyeccion materialice la ficha (404 = el worker todavia no la
    // aplico). Devuelve no nulo o lanza TimeoutException: ningun caller afirma NotBeNull.
    private Task<FichaSedeRespuestaSmoke> EsperarFichaAsync(string codigo, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var response = await _client.GetAsync(Ruta(codigo), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            return await response.Content.ReadFromJsonAsync<FichaSedeRespuestaSmoke>(
                JsonOptions, cancellationToken: ct);
        }, Timeout);

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
    public async Task ObtenerFichaSede_Retorna200ConElShapeBasico_CuandoLaSedeFueRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = NuevoCodigo();

        await RegistrarSedeAsync(codigo, "[TEST] Sede Norte", ct);

        var respuesta = await EsperarFichaAsync(codigo, ct);

        respuesta.Id.Should().Be(ComputarStreamId(codigo));
        respuesta.Codigo.Should().Be(codigo);
        respuesta.Nombre.Should().Be("[TEST] Sede Norte");
        respuesta.CentroDeCostos.Should().BeNull();
        respuesta.Activa.Should().BeTrue("la sede nace activa (CA-1)");
        respuesta.Dispositivos.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaSede_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta(NuevoCodigo()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    // Mismo punto unico de conversion que los comandos del ciclo de vida (CodigoSedeDeRuta.EsValido,
    // MEF-ADR-0037 seccion 2).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"TEST!{Guid.CreateVersion7()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
