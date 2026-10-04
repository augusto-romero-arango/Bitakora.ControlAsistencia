using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.AplicarPlantillaSemanalPorGrupo;

public class AplicarPlantillaSemanalPorGrupoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantillaSemanalPorGrupo_ProgramaALosColaboradoresDeLaSede_CuandoLaPlantillaEsCompletaYNoSeIndicaSedeDeProgramacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";
        var nombrePlantilla = $"[TEST] Plantilla MCP {sufijo}";
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (_, codigoUno) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var (_, codigoDos) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);

        (await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct))
            .IsError.Should().NotBeTrue();
        await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        (await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct)).IsError.Should().NotBeTrue();

        string[] diasDeLaSemana = ["lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo"];
        var dias = JsonSerializer.Serialize(
            diasDeLaSemana.Select(dia => new { semana = 1, dia, turno = nombreTurno }));
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();
        using (var textoCreada = Sembrado.LeerJson(creada))
            textoCreada.RootElement.TryGetProperty("diasRechazados", out _).Should().BeFalse();

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-10-05",
            ["hasta"] = "2026-10-11",
            ["plantilla"] = nombrePlantilla,
            ["sede"] = codigoSede
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "aplicar_plantilla_semanal_por_grupo", argumentos, cancellationToken: ct);
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
                    && programados.GetArrayLength() == 2)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        var resultado = documento.RootElement;
        resultado.GetProperty("plantilla").GetString().Should().Be(nombrePlantilla);
        resultado.GetProperty("ventana").GetString().Should().Be("2026-10-05 a 2026-10-11");
        resultado.GetProperty("grupoResuelto").GetInt32().Should().Be(2);
        resultado.GetProperty("programados").EnumerateArray()
            .Select(p => p.GetProperty("codigoColaborador").GetString())
            .Should().BeEquivalentTo([codigoUno, codigoDos]);
        resultado.GetProperty("programados").EnumerateArray()
            .Should().AllSatisfy(p => p.GetProperty("dias").GetInt32().Should().Be(7));
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantillaSemanalPorGrupo_RespondeElMensajeDeValidacion_CuandoNoLlegaSedeNiEtiquetas()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "aplicar_plantilla_semanal_por_grupo",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-05",
                ["hasta"] = "2026-10-11",
                ["plantilla"] = "[TEST] Plantilla que no existe"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("Indica al menos un criterio de grupo: 'sede' o 'etiquetas'.");
    }
}
