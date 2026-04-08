using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.SmokeTests.Fixtures;

namespace Bitakora.ControlAsistencia.ControlHoras.SmokeTests.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction;

// HU-12: AsignarTurnoCuandoProgramacionTurnoDiarioSolicitada es un trigger de Service Bus.
// No expone ningun endpoint HTTP propio - es un consumidor de eventos del dominio Programacion.
// Lo que se puede verificar en smoke tests:
//   1. El Function App de ControlHoras esta desplegado y disponible (health check).
//   2. El dominio responde correctamente (gateway check).
// La ejecucion real del handler se activa cuando Programacion publica ProgramacionTurnoDiarioSolicitada
// en el topic de Service Bus; ese flujo se verifica con tests de integracion end-to-end.
public class AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaSmokeTests(ApiFixture api)
{
    private readonly HttpClient _client = api.Client;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ControlHoras_DebeEstarDisponible_CuandoSeConsultaHealthCheck()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/health", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ControlHoras_DebeRetornar200_CuandoHealthCheckResponde()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/health", ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNullOrEmpty();
    }
}
