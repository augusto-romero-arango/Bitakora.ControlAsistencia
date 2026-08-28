// Issue #461: smoke tests de ObtenerFichaSede, GET sedes/fichas/{codigo}. Function GET read-side
// sobre la proyeccion FichaSede (receta N1, MEF-ADR-0034/0035).
//
// Arrange via API, nunca sembrando el event store por fuera de ella: la sede se crea con POST sedes
// (#456) -- el mismo comando que la proyeccion consume.
//
// Lifecycle Async (MEF-ADR-0034 seccion 3): el worker materializa FichaSede DESPUES de que Sedes
// persiste sus eventos. El camino feliz envuelve la consulta en Polling.WaitUntilAsync (timeout
// estandar 30s) -- unica excepcion documentada al "no usar Polling directo en tests".
//
// No se repite aqui el detalle de los 9 tipos de evento que alimentan la proyeccion (CA-2/CA-3/CA-4
// del issue): esas reglas de negocio ya las cubre el unit test de FichaSedeProjection
// (projection-test-writer). Este smoke test es black-box: solo verifica que el endpoint desplegado
// responde con el shape basico de la vista materializada y que los bordes 404/400 funcionan contra
// el entorno real.
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

    // Forma local DESACOPLADA del read model de produccion (ReadModels.Sedes.FichaSede): replica
    // solo el shape JSON de la respuesta HTTP de este endpoint. El smoke test no referencia
    // ReadModels ni el Function App (isla, MEF-ADR-0034 seccion 5).
    private sealed record FichaSedeRespuestaSmoke(
        string Id,
        string Codigo,
        string Nombre,
        string? Ciudad,
        string? Direccion,
        string? CentroDeCostos,
        bool Activa,
        IReadOnlyList<string> Dispositivos);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Oraculo independiente de la clave de stream (MEF-ADR-0002): mismo formato que
    // SedeAggregateRoot.ComputarStreamId ("s:{codigo}", CA-ADR-0031), reconstruido localmente sin
    // referenciar el Function App.
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
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que RegistrarSede funcione");
    }

    // Reintenta el GET hasta que la proyeccion asincrona materialice la ficha (404 = el worker
    // todavia no la aplico). Devuelve un valor no nulo o lanza TimeoutException -- por eso ningun
    // caller afirma NotBeNull.
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

    // CA-1/CA-5: registrar una sede nueva materializa la ficha con el shape basico esperado -- nace
    // activa, sin CC ni dispositivos.
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

    // CA-5: ficha inexistente -> 404 sin body.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaSede_Retorna404SinBody_CuandoLaFichaNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta(NuevoCodigo()), ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(ct)).Should().BeEmpty();
    }

    // {codigo} de ruta fuera del charset URL-safe -> 400, mismo punto unico de conversion que ya
    // usan los comandos del ciclo de vida (CodigoSedeDeRuta.EsValido, MEF-ADR-0037 seccion 2).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ObtenerFichaSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync(Ruta($"TEST!{Guid.CreateVersion7()}"), ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
