using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.ModificarVentanaDeEntrada;

public class ModificarVentanaDeEntradaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private const string VentanaEsperada = "entra entre 07:00 y 10:00 (3 h)";

    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    private async Task<object?> FranjaVisibleEnFichaAsync(string turnoId, CancellationToken ct)
    {
        var respuesta = await programacion.Client.GetAsync($"/api/programacion/turnos/{turnoId}", ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        return documento.RootElement.GetProperty("franjas").GetArrayLength() == 1 ? new object() : null;
    }

    private async Task<string?> VentanaEnObtenerTurnoAsync(string turnoId, CancellationToken ct)
    {
        var resultado = await mcp.Cliente.CallToolAsync(
            "obtener_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
        using var documento = JsonDocument.Parse(TextoDe(resultado));
        return documento.RootElement.TryGetProperty("ventanaDeEntrada", out var ventana) ? ventana.GetString() : null;
    }

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
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "07:00", ["fin"] = "16:00" },
            cancellationToken: ct);
        agregada.IsError.Should().NotBeTrue();
        // El eco toma la apertura de la ficha que lee la tool: sin la franja proyectada, sale sin horas.
        await Polling.WaitUntilAsync(() => FranjaVisibleEnFichaAsync(turnoId, ct), CatalogoDeTurnos.TimeoutPolling);

        var modificada = await mcp.Cliente.CallToolAsync(
            "modificar_ventana_de_entrada",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["amplitud"] = "3:00" },
            cancellationToken: ct);
        modificada.IsError.Should().NotBeTrue();
        using var eco = JsonDocument.Parse(TextoDe(modificada));
        eco.RootElement.GetProperty("turno").GetString().Should().Be(nombreTurno);
        eco.RootElement.GetProperty("ventanaDeEntrada").GetString().Should().Be(VentanaEsperada);

        var ventana = await Polling.WaitUntilAsync(
            () => VentanaEnObtenerTurnoAsync(turnoId, ct), CatalogoDeTurnos.TimeoutPolling);
        ventana.Should().Be(VentanaEsperada);

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }
}
