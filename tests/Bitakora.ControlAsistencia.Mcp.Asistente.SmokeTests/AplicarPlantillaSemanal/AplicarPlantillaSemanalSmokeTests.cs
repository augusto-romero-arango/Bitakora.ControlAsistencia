using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AplicarPlantillaSemanal;

public class AplicarPlantillaSemanalSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantillaSemanal_ProgramaLosSieteDias_CuandoLaPlantillaEsCompletaYNoSeIndicaSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";
        var nombrePlantilla = $"[TEST] Plantilla MCP {sufijo}";
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (identificacion, codigoColaborador) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);

        var creadoTurno = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creadoTurno.IsError.Should().NotBeTrue();
        await programacion.Client.EsperarFichaAsync(nombreTurno, ct);

        var conFranja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct);
        conFranja.IsError.Should().NotBeTrue();

        string[] diasDeLaSemana = ["lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo"];
        var dias = JsonSerializer.Serialize(
            diasDeLaSemana.Select(dia => new { semana = 1, dia, turno = nombreTurno }));
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();
        using (var textoCreada = Sembrado.LeerJson(creada))
            textoCreada.RootElement.TryGetProperty("diasRechazados", out _)
                .Should().BeFalse("la plantilla sembrada debe quedar completa para poder aplicarse");

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-10-05",
            ["hasta"] = "2026-10-11",
            ["plantilla"] = nombrePlantilla,
            ["identificaciones"] = identificacion
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "aplicar_plantilla_semanal", argumentos, cancellationToken: ct);
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
        var programado = resultado.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("identificacion").GetString().Should().Be(identificacion);
        programado.GetProperty("dias").GetInt32().Should().Be(7);
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse("no debe haber fallidos");
        (!resultado.TryGetProperty("sede", out var sede) || sede.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("no hubo sede explicita");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantillaSemanal_RespondeElMensajeDeValidacion_CuandoLaVentanaExcedeElMaximo()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "aplicar_plantilla_semanal",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-07",
                ["hasta"] = "2026-11-11",
                ["plantilla"] = "[TEST] Plantilla que no existe",
                ["identificaciones"] = "CC-1"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("La ventana no puede superar 35 dias; se recibieron 36.");
    }
}
