using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.RegistrarSede;

public class RegistrarSedeSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Sedes -> event store. Codigo unico por corrida (TEST-{Guid v7}) para no colisionar con
    // otras ejecuciones del mismo tenant-smoke (CA-ADR-0030: 409 si el codigo ya existe).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_RegistraLaSede_CuandoElCodigoYNombreSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigo = $"TEST-{Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = codigo, ["nombre"] = "[TEST] Sede MCP" },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().Contain(codigo);
    }

    // Error path que no toca el dominio: la validacion de campo en blanco corta en el worker y
    // responde el mensaje del .resx CampoObligatorio en produccion. Afirmar el texto exacto prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarSede_RespondeElMensajeDeValidacion_CuandoElCodigoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = "   ", ["nombre"] = "[TEST] Sede MCP" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'codigo' es obligatorio.");
    }
}
