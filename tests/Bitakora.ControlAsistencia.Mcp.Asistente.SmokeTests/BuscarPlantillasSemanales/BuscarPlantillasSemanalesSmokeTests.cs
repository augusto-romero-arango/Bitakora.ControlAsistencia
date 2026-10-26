using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.BuscarPlantillasSemanales;

public class BuscarPlantillasSemanalesSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarPlantillasSemanales_DevuelveLaPlantillaConMotivoDeTurno_CuandoSeBuscaPorTurnoYSemanas()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7().ToString("N");
        var turno = $"[TEST] Turno Buscar {sufijo}";
        var plantilla = $"[TEST] Plantilla Buscar {sufijo}";

        var turnoCreado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = turno }, cancellationToken: ct);
        turnoCreado.IsError.Should().NotBeTrue();
        using var fichaTurno = await programacion.Client.EsperarFichaAsync(turno, ct);
        var turnoId = fichaTurno.RootElement.GetProperty("id").GetString()!;

        var franja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct);
        franja.IsError.Should().NotBeTrue();

        var dias = $$"""[{"semana":1,"dia":"lunes","turno":"{{turno}}"}]""";
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = plantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();
        using var textoCreada = JsonDocument.Parse(creada.Content.OfType<TextContentBlock>().Single().Text);
        var plantillaId = textoCreada.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;

        try
        {
            var encontrada = await Polling.WaitUntilAsync(async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "buscar_plantillas_semanales",
                    new Dictionary<string, object?> { ["turno"] = sufijo, ["semanas"] = "1" },
                    cancellationToken: ct);
                resultado.IsError.Should().NotBeTrue();
                var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
                if (!texto.StartsWith('{'))
                    return null;

                var documento = JsonDocument.Parse(texto);
                return documento.RootElement.GetProperty("plantillas").GetArrayLength() > 0 ? documento : null;
            }, TimeSpan.FromSeconds(60));

            using (encontrada)
            {
                var candidata = encontrada!.RootElement.GetProperty("plantillas")[0];
                candidata.GetProperty("nombre").GetString().Should().Be(plantilla);
                candidata.GetProperty("coincidePor").EnumerateArray().Select(m => m.GetString())
                    .Should().Contain(m => m!.StartsWith("turno:"));
            }
        }
        finally
        {
            await mcp.Cliente.CallToolAsync(
                "retirar_plantilla_semanal", new Dictionary<string, object?> { ["plantilla_id"] = plantillaId },
                cancellationToken: ct);
            await mcp.Cliente.CallToolAsync(
                "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
        }
    }
}
