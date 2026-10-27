// Lifecycle Async (MEF-ADR-0034 seccion 3): el listado reintenta con Polling hasta que el worker
// materialice (o borre) la vista. Cada test corre en un tenant propio (CA-ADR-0040) y filtra por
// Id unico, nunca por conteo ni posicion.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.ListarCuadrosDeTurnos;

public class ListarCuadrosDeTurnosSmokeTests(ApiFixture api) : IDisposable
{
    private readonly TenantDeTest _tenant = new(api);

    public void Dispose() => _tenant.Dispose();

    private HttpClient _client => _tenant.Client;

    private const string RutaPlantillas = "/api/programacion/plantillas-turnos";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed record CuadroDeTurnosRespuestaSmoke(
        string Id,
        string Nombre,
        int DiasDelCiclo,
        bool Completa);

    private sealed record ListaSmoke(
        IReadOnlyList<CuadroDeTurnosRespuestaSmoke> Elementos, string? SiguienteCursor);

    private async Task<(Guid PlantillaId, string Nombre)> CrearPlantillaAsync(string prefijo, CancellationToken ct)
    {
        var plantillaId = Guid.CreateVersion7();
        var nombre = $"{prefijo} {plantillaId}";

        var response = await _client.PostAsJsonAsync(
            RutaPlantillas, new { plantillaId, nombre, diasDelCiclo = 7 }, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearPlantillaTurnos funcione");

        return (plantillaId, nombre);
    }

    private async Task<ListaSmoke> ListarPaginaAsync(string query, CancellationToken ct)
    {
        var response = await _client.GetAsync($"{RutaPlantillas}{query}", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var lista = await response.Content.ReadFromJsonAsync<ListaSmoke>(JsonOptions, cancellationToken: ct);
        return lista!;
    }

    private Task<List<CuadroDeTurnosRespuestaSmoke>> ListarHastaQueAsync(
        Func<List<CuadroDeTurnosRespuestaSmoke>, bool> condicion, CancellationToken ct) =>
        Polling.WaitUntilAsync(async () =>
        {
            var lista = await ListarPaginaAsync("", ct);
            lista.SiguienteCursor.Should().BeNull("sin take el endpoint devuelve todo el catalogo");
            List<CuadroDeTurnosRespuestaSmoke> elementos = [.. lista.Elementos];
            return condicion(elementos) ? elementos : null;
        }, Timeout);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosDeTurnos_IncluyeElCuadroCreado_CuandoLaPlantillaExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, nombre) = await CrearPlantillaAsync("[TEST] Plantilla Listado Cuadro", ct);

        var lista = await ListarHastaQueAsync(l => l.Any(c => c.Id == plantillaId.ToString()), ct);

        var cuadro = lista.Should().ContainSingle(c => c.Id == plantillaId.ToString()).Subject;
        cuadro.Nombre.Should().Be(nombre);
        cuadro.DiasDelCiclo.Should().Be(7);
        cuadro.Completa.Should().BeFalse("la plantilla no tiene ningun dia asignado");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosDeTurnos_YaNoIncluyeElCuadro_CuandoLaPlantillaFueRetirada()
    {
        var ct = TestContext.Current.CancellationToken;
        var (plantillaId, _) = await CrearPlantillaAsync("[TEST] Plantilla Retirada Cuadro", ct);
        await ListarHastaQueAsync(l => l.Any(c => c.Id == plantillaId.ToString()), ct);

        var retiro = await _client.DeleteAsync($"{RutaPlantillas}/{plantillaId}", ct);
        retiro.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "el arrange de este smoke test depende de que RetirarPlantillaTurnos funcione");

        var lista = await ListarHastaQueAsync(l => l.All(c => c.Id != plantillaId.ToString()), ct);

        lista.Should().NotContain(c => c.Id == plantillaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosDeTurnos_RecorreDosPaginasSinRepetir_CuandoSeUsaTakeYCursor()
    {
        var ct = TestContext.Current.CancellationToken;
        var (primeraId, _) = await CrearPlantillaAsync("[TEST] AAA Pagina Cuadro", ct);
        var (segundaId, _) = await CrearPlantillaAsync("[TEST] ZZZ Pagina Cuadro", ct);
        await ListarHastaQueAsync(
            l => l.Any(c => c.Id == primeraId.ToString()) && l.Any(c => c.Id == segundaId.ToString()), ct);

        var primera = await ListarPaginaAsync("?take=1", ct);
        primera.Elementos.Select(c => c.Id).Should().Equal(primeraId.ToString());
        primera.SiguienteCursor.Should().NotBeNullOrEmpty();

        var segunda = await ListarPaginaAsync(
            $"?take=1&cursor={Uri.EscapeDataString(primera.SiguienteCursor!)}", ct);
        segunda.Elementos.Select(c => c.Id).Should().Equal(segundaId.ToString());
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ListarCuadrosDeTurnos_Retorna400_CuandoElCursorEsInvalido()
    {
        var response = await _client.GetAsync($"{RutaPlantillas}?cursor=no-es-un-cursor",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
