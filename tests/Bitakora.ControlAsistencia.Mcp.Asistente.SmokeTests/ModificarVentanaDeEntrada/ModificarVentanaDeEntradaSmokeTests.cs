using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ModificarVentanaDeEntrada;

public class ModificarVentanaDeEntradaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task ModificarVentanaDeEntrada_ModificaLaVentanaYSeVeEnObtenerTurno_CuandoElTurnoTieneUnaFranja()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Ventana MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();
        using var ficha = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = ficha.RootElement.GetProperty("id").GetString()!;

        var agregada = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "07:00", ["fin"] = "16:00" },
            cancellationToken: ct);
        agregada.IsError.Should().NotBeTrue();

        // La ficha leida por la tool puede no tener aun la franja: se reintenta hasta que el eco
        // trae las horas de apertura y cierre.
        using var eco = await Polling.WaitUntilAsync(
            async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "modificar_ventana_de_entrada",
                    new Dictionary<string, object?> { ["turno"] = nombreTurno, ["amplitud"] = "3:00" },
                    cancellationToken: ct);
                var documento = JsonDocument.Parse(TextoDe(resultado));
                return documento.RootElement.GetProperty("ventanaDeEntrada").GetString()
                    == "entra entre 07:00 y 10:00 (3 h)"
                    ? documento
                    : null;
            },
            CatalogoDeTurnos.TimeoutPolling);
        eco.RootElement.GetProperty("turno").GetString().Should().Be(nombreTurno);

        var visto = await Polling.WaitUntilAsync(
            async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "obtener_turno", new Dictionary<string, object?> { ["id"] = turnoId }, cancellationToken: ct);
                var documento = JsonDocument.Parse(TextoDe(resultado));
                return documento.RootElement.TryGetProperty("ventanaDeEntrada", out var v)
                    && v.GetString() == "entra entre 07:00 y 10:00 (3 h)"
                    ? new object()
                    : null;
            },
            CatalogoDeTurnos.TimeoutPolling);
        visto.Should().NotBeNull();

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }
}
