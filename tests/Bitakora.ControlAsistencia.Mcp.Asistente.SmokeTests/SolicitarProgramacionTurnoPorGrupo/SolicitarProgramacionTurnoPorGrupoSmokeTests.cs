using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.SolicitarProgramacionTurnoPorGrupo;

public class SolicitarProgramacionTurnoPorGrupoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task SembrarTurnoAsync(string nombre, CancellationToken ct)
    {
        var turnoId = Guid.CreateVersion7();
        var respuesta = await programacion.Client.PostAsJsonAsync(
            "/api/programacion/turnos", new { turnoId, nombre }, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);

        var franja = await programacion.Client.PostAsJsonAsync(
            $"/api/programacion/turnos/{turnoId}:agregar-franja",
            new { inicio = "08:00:00", fin = "16:00:00" }, ct);
        franja.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await Polling.WaitUntilAsync(
            async () =>
            {
                using var candidata = await programacion.Client.BuscarFichaAsync(nombre, ct);
                return candidata?.RootElement.GetProperty("completo").GetBoolean() == true ? new object() : null;
            }, TimeoutPolling);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoPorGrupo_ProgramaALosDosColaboradoresDeLaSede_CuandoElGrupoSeDefinePorSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (_, codigoUno) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var (_, codigoDos) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";
        await SembrarTurnoAsync(nombreTurno, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno"] = nombreTurno,
            ["sede_de_programacion"] = codigoSede,
            ["sede"] = codigoSede
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno_por_grupo", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                if (candidato.RootElement.TryGetProperty("programados", out var programados)
                    && programados.GetArrayLength() == 2)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var resultado = documento.RootElement;
        resultado.GetProperty("turno").GetString().Should().Be(nombreTurno);
        resultado.GetProperty("grupoResuelto").GetInt32().Should().Be(2);
        resultado.GetProperty("programados").EnumerateArray()
            .Select(p => p.GetProperty("codigoColaborador").GetString())
            .Should().BeEquivalentTo([codigoUno, codigoDos]);
        resultado.GetProperty("programados").EnumerateArray()
            .Should().AllSatisfy(p => p.GetProperty("dias").GetInt32().Should().Be(3));
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoPorGrupo_RespondeElRechazo_CuandoUnaEtiquetaEstaMalFormada()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "solicitar_programacion_turno_por_grupo",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-09-03",
                ["turno"] = "[TEST] Turno que no existe",
                ["sede_de_programacion"] = "TEST-INEXISTENTE",
                ["etiquetas"] = "area"
            },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("La etiqueta 'area' no tiene la forma categoria:valor.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurnoPorGrupo_ProgramaConLaSedeDeCadaColaborador_CuandoNoSeIndicaSedeDeProgramacion()
    {
        var ct = TestContext.Current.CancellationToken;
        var codigoSede = await Sembrado.RegistrarSedeAsync(mcp.Cliente, ct);
        var (_, codigoUno) = await Sembrado.RegistrarColaboradorAsync(mcp.Cliente, codigoSede, ct);
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";
        await SembrarTurnoAsync(nombreTurno, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno"] = nombreTurno,
            ["sede"] = codigoSede
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno_por_grupo", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                if (candidato.RootElement.TryGetProperty("programados", out var programados)
                    && programados.EnumerateArray().Any(p => p.GetProperty("codigoColaborador").GetString() == codigoUno))
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        documento.RootElement.TryGetProperty("fallidos", out _).Should().BeFalse();
        (!documento.RootElement.TryGetProperty("sede", out var sedeNivelSuperior)
            || sedeNivelSuperior.ValueKind == JsonValueKind.Null)
            .Should().BeTrue("no hubo sede explicita");
    }
}
