using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AplicarPlantilla;

public class AplicarPlantillaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantilla_ProgramaLaVentanaDesdeLaFechaDeInicio_CuandoLaPlantillaEsCompleta()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";
        var nombrePlantilla = $"[TEST] Plantilla MCP {sufijo}";
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (identificacion, codigoColaborador) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);

        (await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct))
            .IsError.Should().NotBeTrue();
        using var fichaTurno = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = Guid.Parse(fichaTurno.RootElement.GetProperty("id").GetString()!);
        (await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId.ToString(), ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct)).IsError.Should().NotBeTrue();

        var plantillaId = await programacion.Client.CrearPlantillaAsync(nombrePlantilla, 3, ct);
        foreach (var dia in new[] { 1, 2, 3 })
            await programacion.Client.AsignarTurnoADiaAsync(plantillaId, dia, turnoId, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["plantilla_id"] = plantillaId.ToString(),
            ["fecha_inicio"] = "2026-10-03",
            ["desde"] = "2026-10-05",
            ["hasta"] = "2026-10-11",
            ["identificaciones"] = identificacion
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "aplicar_plantilla", argumentos, cancellationToken: ct);
                JsonDocument candidato;
                try
                {
                    candidato = JsonDocument.Parse(TextoDe(respuesta));
                }
                catch (JsonException)
                {
                    return null;
                }

                if (candidato.RootElement.ValueKind == JsonValueKind.Object
                    && candidato.RootElement.TryGetProperty("programados", out var programados)
                    && programados.EnumerateArray().Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador))
                    return candidato;

                candidato.Dispose();
                return null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        var resultado = documento.RootElement;
        resultado.GetProperty("plantilla").GetString().Should().Be(nombrePlantilla);
        resultado.GetProperty("ventana").GetString().Should().Be("2026-10-05 a 2026-10-11");
        resultado.GetProperty("fechaInicio").GetString().Should().Be("2026-10-03");
        var programado = resultado.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("dias").GetInt32().Should().Be(7);
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse("no debe haber fallidos");
    }
}
