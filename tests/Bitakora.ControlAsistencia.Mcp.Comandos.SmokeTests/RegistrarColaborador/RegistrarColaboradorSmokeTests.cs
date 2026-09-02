using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.RegistrarColaborador;

public class RegistrarColaboradorSmokeTests(McpFixture mcp)
{
    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Colaboradores -> event store. Numero de identificacion (mismo oraculo que
    // RegistrarColaboradorSmokeTests de Colaboradores: Guid.CreateVersion7 en mayusculas) y codigo
    // unicos por corrida para no colisionar con otras ejecuciones del mismo tenant-smoke
    // (CA-ADR-0030: 409 si la identificacion ya existe).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_RegistraElColaborador_CuandoLosDatosSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var numero = Guid.CreateVersion7().ToString("N").ToUpperInvariant();
        var codigo = $"TEST-{Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = numero,
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = codigo,
                ["fecha_inicio"] = "2026-09-01"
            },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().Contain(codigo);
    }

    // Error path que no toca el dominio: la validacion de formato de fecha_inicio corta en el
    // worker y responde el mensaje del .resx FechaInvalida en produccion. Afirmar el texto exacto
    // prueba que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_RespondeElMensajeDeValidacion_CuandoFechaInicioTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = Guid.CreateVersion7().ToString("N").ToUpperInvariant(),
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = $"TEST-{Guid.CreateVersion7()}",
                ["fecha_inicio"] = "2026-99-99"
            },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'fecha_inicio' debe tener formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
