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
        using var fichaTurno = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = fichaTurno.RootElement.GetProperty("id").GetString()!;

        var conFranja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct);
        conFranja.IsError.Should().NotBeTrue();

        string[] diasDeLaSemana = ["lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo"];
        var dias = JsonSerializer.Serialize(
            diasDeLaSemana.Select(dia => new { dia, turno_id = turnoId }));
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();
        string plantillaId;
        using (var textoCreada = Sembrado.LeerJson(creada))
        {
            textoCreada.RootElement.TryGetProperty("diasRechazados", out _)
                .Should().BeFalse("la plantilla sembrada debe quedar completa para poder aplicarse");
            plantillaId = textoCreada.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;
        }

        var argumentos = new Dictionary<string, object?>
        {
            ["desde"] = "2026-10-05",
            ["hasta"] = "2026-10-11",
            ["plantilla_id"] = plantillaId,
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

        await mcp.Cliente.CallToolAsync(
            "retirar_plantilla", new Dictionary<string, object?> { ["plantilla_id"] = plantillaId }, cancellationToken: ct);
        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AplicarPlantillaSemanal_RemiteAAplicarPlantilla_CuandoElCicloNoEsDeSieteDias()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";

        (await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct))
            .IsError.Should().NotBeTrue();
        using var fichaTurno = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = fichaTurno.RootElement.GetProperty("id").GetString()!;
        (await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct)).IsError.Should().NotBeTrue();

        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla",
            new Dictionary<string, object?>
            {
                ["nombre"] = $"[TEST] Ciclo MCP {sufijo}",
                ["dias_del_ciclo"] = 3,
                ["dias"] = $$"""[{"dia":1,"turno_id":"{{turnoId}}"},{"dia":2,"turno_id":"{{turnoId}}"},{"dia":3,"turno_id":"{{turnoId}}"}]"""
            },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();
        string plantillaId;
        using (var textoCreada = Sembrado.LeerJson(creada))
            plantillaId = textoCreada.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;

        var rechazo = await Polling.WaitUntilAsync(
            async () =>
            {
                var respuesta = await mcp.Cliente.CallToolAsync(
                    "aplicar_plantilla_semanal",
                    new Dictionary<string, object?>
                    {
                        ["desde"] = "2026-10-05",
                        ["hasta"] = "2026-10-11",
                        ["plantilla_id"] = plantillaId,
                        ["identificaciones"] = "CC-1"
                    },
                    cancellationToken: ct);
                var texto = TextoDe(respuesta);
                return texto.Contains("no es semanal") ? texto : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

        rechazo.Should().Contain("aplicar_plantilla");

        await mcp.Cliente.CallToolAsync(
            "retirar_plantilla", new Dictionary<string, object?> { ["plantilla_id"] = plantillaId }, cancellationToken: ct);
        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }
}
