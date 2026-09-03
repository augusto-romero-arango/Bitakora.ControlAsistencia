using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.SolicitarProgramacionTurno;

public class SolicitarProgramacionTurnoSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private static object PayloadTurno(Guid turnoId, string nombre) => new
    {
        turnoId,
        nombre,
        ordinarias = new[]
        {
            new
            {
                inicio = "08:00:00",
                fin = "16:00:00",
                descansos = Array.Empty<object>(),
                extras = Array.Empty<object>()
            }
        }
    };

    private async Task SembrarTurnoAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.Client.PostAsJsonAsync(
            "/api/programacion/turnos", PayloadTurno(turnoId, nombre), ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Accepted,
            "el arrange de este smoke test depende de que CrearTurno funcione en Programacion");
    }

    // Recorre la cadena completa: host MCP -> worker -> 3 HttpClients tipados -> Function Apps de
    // Sedes/Colaboradores/Programacion -> event store. El assert vive DENTRO del polling por el
    // lifecycle Async del directorio -- ver Fixtures/Polling.cs.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_ProgramaAlColaborador_CuandoLaVentanaCubreSuVigencia()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        var codigoColaborador = $"TEST-{sufijo}";
        var numeroIdentificacion = sufijo.ToString("N").ToUpperInvariant();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";

        await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?>
            {
                ["codigo"] = codigoSede,
                ["nombre"] = "[TEST] Sede MCP Programacion"
            },
            cancellationToken: ct);
        await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            new Dictionary<string, object?>
            {
                ["tipo_identificacion"] = "CC",
                ["numero_identificacion"] = numeroIdentificacion,
                ["primer_nombre"] = "[TEST]",
                ["primer_apellido"] = "MCP",
                ["codigo_colaborador"] = codigoColaborador,
                ["fecha_inicio"] = "2026-09-01"
            },
            cancellationToken: ct);
        await SembrarTurnoAsync(Guid.CreateVersion7(), nombreTurno, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno"] = nombreTurno,
            ["sede_de_programacion"] = codigoSede,
            ["identificaciones"] = $"CC-{numeroIdentificacion}"
        };

        // JsonDocument (no JsonElement): WaitUntilAsync<T> exige T : class -- JsonElement es struct.
        var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno", argumentos, cancellationToken: ct);
                var texto = respuesta.Content.OfType<TextContentBlock>().Single().Text;
                var candidato = JsonDocument.Parse(texto);

                var contieneAlColaborador = candidato.RootElement.GetProperty("programados").EnumerateArray()
                    .Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);

                if (contieneAlColaborador)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);
        using var documentoDisponible = documento;
        var resultado = documento.RootElement;

        var programado = resultado.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("dias").GetInt32().Should().Be(3);
        resultado.GetProperty("omitidos").GetInt32().Should().Be(0);
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse("no debe haber fallidos");
    }

    // Error path que no toca ningun dominio: la ventana de 32 dias corta en el worker y responde el
    // mensaje del .resx VentanaExcedeMaximo -- prueba que los recursos embebidos viajaron en el
    // publish, mismo criterio que el resto de error paths de esta suite.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_RespondeElMensajeDeValidacion_CuandoLaVentanaExcedeElMaximo()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "solicitar_programacion_turno",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-09-01",
                ["hasta"] = "2026-10-02",
                ["turno"] = "[TEST] Turno que no existe",
                ["sede_de_programacion"] = "TEST-INEXISTENTE",
                ["identificaciones"] = "CC-1"
            },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("La ventana no puede superar 31 dias; se recibieron 32.");
    }
}
