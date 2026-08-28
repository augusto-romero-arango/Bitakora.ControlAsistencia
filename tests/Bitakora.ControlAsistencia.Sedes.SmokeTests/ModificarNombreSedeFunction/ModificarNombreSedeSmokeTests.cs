// Issue #457: smoke tests del endpoint PUT sedes/{codigo}/nombre. Comando event-sourcing puro sin
// consumidores downstream (CA-ADR-0030): NombreSedeModificado no cruza el bus en este issue, asi
// que no hay ServiceBusFixture -- la unica verificacion black-box de los efectos del handler es
// leer mt_events via PostgresFixture.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.Sedes.SmokeTests.ModificarNombreSedeFunction;

public class ModificarNombreSedeSmokeTests(ApiFixture api, PostgresFixture postgres)
{
    private readonly HttpClient _client = api.Client;

    private const string RutaRegistrar = "/api/sedes";
    private const string SchemaSedes = "sedes";
    private const string TipoEventoSedeRegistrada = "sede_registrada";
    private const string TipoEventoNombreSedeModificado = "nombre_sede_modificado";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Prefijo "TEST-" en vez de "[TEST] ": Codigo esta sujeto al charset URL-safe (CA-4 de #456) y
    // "[", "]" y el espacio quedan fuera de ese set.
    private static string NuevoCodigo() => $"TEST-{Guid.CreateVersion7()}";

    // Recomputo local del streamId (oraculo independiente, MEF-ADR-0002): no se referencia
    // SedeAggregateRoot.ComputarStreamId desde el smoke test.
    private static string ComputarStreamId(string codigo) => $"s:{codigo}";

    private static string RutaNombre(string codigo) => $"/api/sedes/{codigo}/nombre";

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
            $"el evento {TipoEventoSedeRegistrada} deberia existir en el stream {streamId} antes de modificar el nombre");

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

    // CA-1: PUT nombre con valor valido persiste NombreSedeModificado.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarNombreSede_Retorna202YPersisteNombreSedeModificado_CuandoNombreEsValido()
    {
        Assert.SkipWhen(!postgres.IsConfigured, postgres.SkipReason ?? "Postgres no disponible.");

        var ct = TestContext.Current.CancellationToken;
        var codigo = await RegistrarSedeDePruebaAsync(ct);

        var payload = new { nombre = "[TEST] Sede Renombrada" };
        var response = await _client.PutAsJsonAsync(RutaNombre(codigo), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var streamId = ComputarStreamId(codigo);
        var existe = await postgres.ExisteEventoAsync(
            SchemaSedes, streamId, TipoEventoNombreSedeModificado, Timeout);

        existe.Should().BeTrue(
            $"el evento {TipoEventoNombreSedeModificado} deberia existir en el stream {streamId}");

        var evento = await postgres.ObtenerEventoAsync<JsonElement>(
            SchemaSedes, streamId, TipoEventoNombreSedeModificado, TimeSpan.FromSeconds(5));

        evento.GetProperty("Nombre").GetString().Should().Be("[TEST] Sede Renombrada");
    }

    // CA-2: Nombre vacio -> 400 (borde), sin evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarNombreSede_Retorna400_CuandoNombreEsVacio()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { nombre = "" };

        var response = await _client.PutAsJsonAsync(RutaNombre(NuevoCodigo()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // CA-4: sede inexistente -> 404, sin evento.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarNombreSede_Retorna404_CuandoSedeNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = new { nombre = "[TEST] Sede Inexistente" };

        var response = await _client.PutAsJsonAsync(RutaNombre(NuevoCodigo()), payload, ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
