// Issue #457: smoke tests del endpoint PUT sedes/{codigo}/ubicacion. Comando event-sourcing puro
// sin consumidores downstream (CA-ADR-0030): UbicacionActualizada no cruza el bus en este issue,
// asi que no hay ServiceBusFixture -- la unica verificacion black-box de los efectos del handler es
// leer mt_events via PostgresFixture.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ActualizarUbicacionSedeFunction;

public class ActualizarUbicacionSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoUbicacionActualizada = "ubicacion_actualizada";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" en vez de "[TEST] ": Codigo esta sujeto al charset URL-safe (CA-4 de #456) y
    // "[", "]" y el espacio quedan fuera de ese set.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId (oraculo independiente, MEF-ADR-0002): no se referencia
    // SedeAggregateRoot.ComputarStreamId desde el smoke test.
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

    // CA-3: PUT ubicacion persiste UbicacionActualizada con ambos campos presentes.
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

    // CA-3: Ciudad y Direccion son opcionales -- se aceptan nulos y se persisten como tal.
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

    // CA-4: sede inexistente -> 404, sin evento.
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
