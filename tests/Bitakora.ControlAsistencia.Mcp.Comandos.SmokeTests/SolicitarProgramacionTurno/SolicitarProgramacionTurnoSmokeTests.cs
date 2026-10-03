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

    private async Task SembrarTurnoAsync(Guid turnoId, string nombre, CancellationToken ct)
    {
        var respuesta = await programacion.Client.PostAsJsonAsync(
            "/api/programacion/turnos", new { turnoId, nombre }, ct);
        respuesta.StatusCode.Should().Be(HttpStatusCode.Created,
            "el arrange de este smoke test depende de que CrearTurno funcione en Programacion");

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
    public async Task SolicitarProgramacionTurno_Programa35Dias_CuandoLaVentanaCubreSuVigencia()
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
                ["fecha_inicio"] = "2026-10-01"
            },
            cancellationToken: ct);
        await SembrarTurnoAsync(Guid.CreateVersion7(), nombreTurno, ct);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-10-07",
            ["hasta"] = "2026-11-10",
            ["turno"] = nombreTurno,
            ["sede_de_programacion"] = codigoSede,
            ["identificaciones"] = $"CC-{numeroIdentificacion}"
        };

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

        resultado.GetProperty("resultado").GetString().Should().Be("Programacion solicitada");
        resultado.GetProperty("turno").GetString().Should().Be(nombreTurno);
        resultado.GetProperty("sede").GetProperty("codigo").GetString().Should().Be(codigoSede);
        resultado.GetProperty("programados").GetArrayLength().Should().Be(1);
        var programado = resultado.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("identificacion").GetString().Should().Be($"CC-{numeroIdentificacion}");
        programado.GetProperty("dias").GetInt32().Should().Be(35);
        programado.GetProperty("desde").GetString().Should().Be("2026-10-07");
        programado.GetProperty("hasta").GetString().Should().Be("2026-11-10");
        resultado.GetProperty("ventana").GetString().Should().Be("2026-10-07 a 2026-11-10");
        resultado.GetProperty("omitidos").GetInt32().Should().Be(0);
        resultado.TryGetProperty("fallidos", out _).Should().BeFalse("no debe haber fallidos");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_InformaLosDiasRespetados_CuandoElColaboradorTieneUnaAusenciaEnLaVentana()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        var codigoColaborador = $"TEST-{sufijo}";
        var numeroIdentificacion = sufijo.ToString("N").ToUpperInvariant();
        var identificacion = $"CC-{numeroIdentificacion}";
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

        using var ausencia = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "programar_ausencia",
                    new Dictionary<string, object?>
                    {
                        ["identificacion"] = identificacion,
                        ["desde"] = "2026-09-02",
                        ["hasta"] = "2026-09-02",
                        ["motivo"] = "Vacaciones"
                    },
                    cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);
                if (candidato.RootElement.TryGetProperty("motivo", out _))
                    return candidato;
                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-09-01",
            ["hasta"] = "2026-09-03",
            ["turno"] = nombreTurno,
            ["sede_de_programacion"] = codigoSede,
            ["identificaciones"] = identificacion
        };

        using var documento = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "solicitar_programacion_turno", argumentos, cancellationToken: ct);
                var candidato = JsonDocument.Parse(respuesta.Content.OfType<TextContentBlock>().Single().Text);

                var contieneAlColaborador = candidato.RootElement.GetProperty("programados").EnumerateArray()
                    .Any(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
                if (contieneAlColaborador)
                    return candidato;

                candidato.Dispose();
                return null;
            },
            TimeoutPolling);

        var programado = documento.RootElement.GetProperty("programados").EnumerateArray()
            .Single(p => p.GetProperty("codigoColaborador").GetString() == codigoColaborador);
        programado.GetProperty("dias").GetInt32().Should().Be(2, "el dia 2026-09-02 se respeta por ausencia");
        var respetado = programado.GetProperty("respetados").EnumerateArray().Single();
        respetado.GetProperty("motivo").GetString().Should().Be("Vacaciones");
        respetado.GetProperty("tramos").GetString().Should().Be("2");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SolicitarProgramacionTurno_RespondeElMensajeDeValidacion_CuandoLaVentanaExcedeElMaximo()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "solicitar_programacion_turno",
            new Dictionary<string, object?>
            {
                ["desde"] = "2026-10-07",
                ["hasta"] = "2026-11-11",
                ["turno"] = "[TEST] Turno que no existe",
                ["sede_de_programacion"] = "TEST-INEXISTENTE",
                ["identificaciones"] = "CC-1"
            },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("La ventana no puede superar 35 dias; se recibieron 36.");
    }
}
