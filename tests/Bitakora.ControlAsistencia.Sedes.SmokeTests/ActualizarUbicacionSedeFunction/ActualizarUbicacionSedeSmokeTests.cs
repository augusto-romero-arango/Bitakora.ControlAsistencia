using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ActualizarUbicacionSedeFunction;

// UbicacionActualizada no cruza el bus: la unica verificacion black-box de los efectos del handler
// es leer mt_events via PostgresFixture -- no hay ServiceBusFixture que consultar.
public class ActualizarUbicacionSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoUbicacionActualizada = "ubicacion_actualizada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" y no "[TEST] ": el Codigo viaja en la ruta y esta sujeto al charset URL-safe,
    // del que "[", "]" y el espacio quedan fuera.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId: oraculo independiente, sin referenciar ComputarStreamId.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaUbicacion(string codigo) => $"/api/sedes/{codigo}/ubicacion";

    private async Task<string> RegistrarSedeDePruebaAsync(CancellationToken ct)
    {
        var codigo = NuevoCodigo();
        var payload = new { codigo, nombre = "[TEST] Sede Original", ciudad = (string?)null, direccion = (string?)null };

        var response = await _client.PostAsJsonAsync(RutaRegistrar, payload, ct);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que el registro previo funcione");

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoSedeRegistrada, Timeout);
        existe.Should().BeTrue(
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de actualizar la ubicacion");

        return codigo;
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // CA-3
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActualizarUbicacionSede_Retorna202YPersisteUbicacionActualizada_CuandoAmbosCamposLlegan()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);

        var payload = new { ciudad = "Medellin", direccion = "Carrera 50 # 10-20" };
        var response = await _client.PutAsJsonAsync(RutaUbicacion(codigo), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoUbicacionActualizada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoUbicacionActualizada} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoUbicacionActualizada, TimeSpan.FromSeconds(5));

        evento.GetProperty("Ciudad").GetString().Should().Be("Medellin");
        evento.GetProperty("Direccion").GetString().Should().Be("Carrera 50 # 10-20");
    }

    // CA-3: los opcionales ausentes se persisten como null, no se omiten del evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActualizarUbicacionSede_Retorna202YPersisteCiudadYDireccionNulos_CuandoNoLlegan()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);

        var payload = new { ciudad = (string?)null, direccion = (string?)null };
        var response = await _client.PutAsJsonAsync(RutaUbicacion(codigo), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoUbicacionActualizada, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoUbicacionActualizada} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoUbicacionActualizada, TimeSpan.FromSeconds(5));

        evento.GetProperty("Ciudad").ValueKind.Should().Be(JsonValueKind.Null);
        evento.GetProperty("Direccion").ValueKind.Should().Be(JsonValueKind.Null);
    }

    // El charset URL-safe del codigo tambien rige cuando viaja en la ruta: "!" queda fuera del set
    // unreserved y se rechaza con 400, nunca con el 404 de un stream inexistente.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActualizarUbicacionSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { ciudad = "Cali", direccion = "Avenida 6 # 3-4" };

        var response = await _client.PutAsJsonAsync(
            RutaUbicacion($"TEST!{Guid.CreateVersion7()}"), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-4
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ActualizarUbicacionSede_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { ciudad = "Cali", direccion = "Avenida 6 # 3-4" };

        var response = await _client.PutAsJsonAsync(RutaUbicacion(NuevoCodigo()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
