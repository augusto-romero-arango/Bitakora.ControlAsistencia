using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.BuscarTurnos;

public class BuscarTurnosSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task BuscarTurnos_DevuelveElTurnoConHorarioExacto_CuandoSeBuscaPorNombreYHorario()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7().ToString("N");
        var nombre = $"[TEST] Buscar Turno {sufijo}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombre }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();
        using var fichaTurno = await programacion.Client.EsperarFichaAsync(nombre, ct);
        var turnoId = fichaTurno.RootElement.GetProperty("id").GetString()!;

        try
        {
            var franja = await mcp.Cliente.CallToolAsync(
                "agregar_franja",
                new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "06:00", ["fin"] = "14:00" },
                cancellationToken: ct);
            franja.IsError.Should().NotBeTrue();

            var encontrado = await Polling.WaitUntilAsync(async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "buscar_turnos",
                    new Dictionary<string, object?> { ["nombre"] = sufijo, ["horario"] = "06:00-14:00" },
                    cancellationToken: ct);
                resultado.IsError.Should().NotBeTrue();
                var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;
                if (!texto.StartsWith('{'))
                    return null;

                var documento = JsonDocument.Parse(texto);
                return documento.RootElement.GetProperty("turnos").GetArrayLength() > 0 ? documento : null;
            }, TimeSpan.FromSeconds(60));

            using (encontrado)
            {
                var turno = encontrado!.RootElement.GetProperty("turnos")[0];
                turno.GetProperty("nombre").GetString().Should().Be(nombre);
                turno.GetProperty("coincidePor").EnumerateArray().Select(m => m.GetString())
                    .Should().Contain(m => m!.StartsWith("horario exacto 06:00-14:00"));
            }
        }
        finally
        {
            await mcp.Cliente.CallToolAsync(
                "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
        }
    }
}
