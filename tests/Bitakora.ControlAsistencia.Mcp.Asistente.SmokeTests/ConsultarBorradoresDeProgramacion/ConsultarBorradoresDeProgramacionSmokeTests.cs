using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ConsultarBorradoresDeProgramacion;

public class ConsultarBorradoresDeProgramacionSmokeTests(McpFixture mcp)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarBorradoresDeProgramacion_Responde_CuandoSeConsultaUnCodigoNuevoEnUnPeriodo()
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_borradores_de_programacion",
            new Dictionary<string, object?>
            {
                ["codigos_colaborador"] = $"TEST-{Guid.NewGuid():N}",
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-30"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        if (texto.TrimStart().StartsWith('{'))
        {
            using var json = JsonDocument.Parse(texto);
            json.RootElement.GetProperty("desde").GetString().Should().Be("2026-09-01");
            json.RootElement.GetProperty("hasta").GetString().Should().Be("2026-09-30");
        }
        else
        {
            texto.Should().StartWith("No hay borrador de programacion pendiente");
        }
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ConsultarBorradoresDeProgramacion_RespondeElMensajeDeValidacion_CuandoLaFechaEsInvalida()
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "consultar_borradores_de_programacion",
            new Dictionary<string, object?> { ["desde"] = "2026-99-99", ["hasta"] = "2026-09-30" },
            cancellationToken: TestContext.Current.CancellationToken);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'desde' debe ser una fecha con formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
