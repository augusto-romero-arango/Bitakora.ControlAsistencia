using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.EditarPlantillaDeTurnos;

public class EditarPlantillaDeTurnosSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EditarPlantillaDeTurnos_AsignaDiaYJornada_LaQuitaYRetiraLaPlantilla()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno Edicion Plantilla {sufijo}";

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

        var nombrePlantilla = $"[TEST] Plantilla Edicion {sufijo}";
        var plantillaId = (await programacion.Client.CrearPlantillaAsync(nombrePlantilla, 3, ct)).ToString();

        var (jornada, _) = await SembradoDeJornadas.CrearAsync(mcp, ct);
        var jornadaId = jornada.GetProperty("jornada_id").GetString()!;

        // La plantilla y el turno se materializan de forma asincrona: se reintenta hasta que la tool la ve.
        var asignado = await Polling.WaitUntilAsync(async () =>
        {
            var r = await mcp.Cliente.CallToolAsync(
                "asignar_turno_a_dia_de_plantilla",
                new Dictionary<string, object?> { ["plantilla_id"] = plantillaId, ["dia"] = 1, ["turno_id"] = turnoId },
                cancellationToken: ct);
            var texto = TextoDe(r);
            return texto.TrimStart().StartsWith('{') ? JsonDocument.Parse(texto) : null;
        }, CatalogoDeTurnos.TimeoutPolling);
        using (asignado)
        {
            asignado.RootElement.GetProperty("plantilla").GetString().Should().Be(nombrePlantilla);
            asignado.RootElement.GetProperty("dia").GetInt32().Should().Be(1);
            asignado.RootElement.GetProperty("turno").GetString().Should().Be(nombreTurno);
        }

        var conJornada = await mcp.Cliente.CallToolAsync(
            "asignar_jornada_a_plantilla",
            new Dictionary<string, object?> { ["plantilla_id"] = plantillaId, ["jornada_id"] = jornadaId },
            cancellationToken: ct);
        conJornada.IsError.Should().NotBeTrue();
        using (var eco = JsonDocument.Parse(TextoDe(conJornada)))
            eco.RootElement.GetProperty("jornada").GetProperty("jornada_id").GetString().Should().Be(jornadaId);

        var sinJornada = await mcp.Cliente.CallToolAsync(
            "quitar_jornada_de_plantilla",
            new Dictionary<string, object?> { ["plantilla_id"] = plantillaId },
            cancellationToken: ct);
        sinJornada.IsError.Should().NotBeTrue();
        using (var eco = JsonDocument.Parse(TextoDe(sinJornada)))
            eco.RootElement.GetProperty("plantilla").GetString().Should().Be(nombrePlantilla);

        var retirada = await mcp.Cliente.CallToolAsync(
            "retirar_plantilla",
            new Dictionary<string, object?> { ["plantilla_id"] = plantillaId },
            cancellationToken: ct);
        retirada.IsError.Should().NotBeTrue();
        using (var eco = JsonDocument.Parse(TextoDe(retirada)))
        {
            eco.RootElement.GetProperty("plantilla").GetProperty("id").GetString().Should().Be(plantillaId);
            eco.RootElement.GetProperty("plantilla").GetProperty("nombre").GetString().Should().Be(nombrePlantilla);
        }

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task EditarPlantillaDeTurnos_RespondePlantillaNoExiste_CuandoElIdNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var inexistente = Guid.CreateVersion7().ToString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "retirar_plantilla",
            new Dictionary<string, object?> { ["plantilla_id"] = inexistente },
            cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        TextoDe(resultado).Should().NotStartWith("{").And.Contain(inexistente);
    }
}
