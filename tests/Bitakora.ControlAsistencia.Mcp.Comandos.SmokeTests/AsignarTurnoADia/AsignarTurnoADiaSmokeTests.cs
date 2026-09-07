using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.AsignarTurnoADia;

// El ciclo crear turno+plantilla -> asignar -> quitar va junto (CA-5 del issue #628, mismo
// precedente que CrearPlantillaSemanalSmokeTests): retirar_plantilla_semanal/retirar_turno limpian
// lo sembrado por este mismo test.
public class AsignarTurnoADiaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // El cuadro se materializa asincronicamente: la vista tarda unos segundos en reflejar el PUT y
    // el DELETE. Un dia sin turno no aparece en "dias" (ausencia = vacio, CuadroSemanalTurnos), asi
    // que la misma sonda sirve para esperar que aparezca y que deje de aparecer.
    private async Task EsperarHastaQueElMartesDeLaPrimeraSemana(string plantillaId, bool aparezca, CancellationToken ct) =>
        await Polling.WaitUntilAsync(
            async () =>
            {
                using var cuadro = await programacion.BuscarCuadroAsync(plantillaId, ct);
                var aparece = cuadro?.RootElement.GetProperty("dias").EnumerateArray()
                    .Any(dia => dia.GetProperty("semana").GetInt32() == 1 && dia.GetProperty("dia").GetInt32() == 2) ?? false;
                return aparece == aparezca ? new object() : null;
            },
            CatalogoDeTurnos.TimeoutPolling);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_MuestraElTurnoEnElDia_YQuitarTurnoDeDia_LoDejaDeMostrar()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var nombreTurno = $"[TEST] Turno MCP {sufijo}";
        var nombrePlantilla = $"[TEST] Plantilla MCP {sufijo}";

        var creadoTurno = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creadoTurno.IsError.Should().NotBeTrue();

        using var fichaTurno = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);

        var conFranja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "06:00", ["fin"] = "14:00" },
            cancellationToken: ct);
        conFranja.IsError.Should().NotBeTrue();

        // Plantilla de un solo dia (lunes): martes queda vacio a proposito -- es el slot que este
        // test asigna y luego quita con las dos tools bajo prueba.
        var dias = $$"""[{"semana":1,"dia":"lunes","turno":"{{nombreTurno}}"}]""";
        var creada = await mcp.Cliente.CallToolAsync(
            "crear_plantilla_semanal",
            new Dictionary<string, object?> { ["nombre"] = nombrePlantilla, ["dias"] = dias },
            cancellationToken: ct);
        creada.IsError.Should().NotBeTrue();

        using var textoCreada = JsonDocument.Parse(TextoDe(creada));
        var plantillaId = textoCreada.RootElement.GetProperty("plantilla").GetProperty("id").GetString()!;

        using (var cuadroInicial = await programacion.EsperarCuadroAsync(plantillaId, ct))
            cuadroInicial.RootElement.GetProperty("dias").GetArrayLength().Should().Be(1);

        var asignado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?>
            {
                ["plantilla"] = nombrePlantilla,
                ["turno"] = nombreTurno,
                ["dia"] = "martes",
                ["semana"] = 1
            },
            cancellationToken: ct);
        asignado.IsError.Should().NotBeTrue();

        await EsperarHastaQueElMartesDeLaPrimeraSemana(plantillaId, aparezca: true, ct);

        var quitado = await mcp.Cliente.CallToolAsync(
            "quitar_turno_de_dia",
            new Dictionary<string, object?> { ["plantilla"] = nombrePlantilla, ["dia"] = "martes", ["semana"] = 1 },
            cancellationToken: ct);
        quitado.IsError.Should().NotBeTrue();

        await EsperarHastaQueElMartesDeLaPrimeraSemana(plantillaId, aparezca: false, ct);

        await mcp.Cliente.CallToolAsync(
            "retirar_plantilla_semanal",
            new Dictionary<string, object?> { ["plantilla"] = nombrePlantilla },
            cancellationToken: ct);
        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }

    // CA-2/CA-4: plantilla inexistente -> PlantillaNoExiste con el catalogo real de dev, sin PUT.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_RespondePlantillaNoExiste_CuandoLaPlantillaNoExisteEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var plantillaInexistente = $"[TEST] Plantilla inexistente {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?>
            {
                ["plantilla"] = plantillaInexistente,
                ["turno"] = "cualquiera",
                ["dia"] = "lunes"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain(plantillaInexistente);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos de cada tool viajaron en el publish (mismo patron que AgregarFranja/
    // QuitarFranja). Ambas tools comparten 'plantilla' como primer chequeo -- un test por tool basta.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarTurnoADia_RespondeElMensajeDeValidacion_CuandoLaPlantillaEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_turno_a_dia",
            new Dictionary<string, object?> { ["plantilla"] = "   ", ["turno"] = "cualquiera", ["dia"] = "lunes" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'plantilla' es obligatorio.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarTurnoDeDia_RespondeElMensajeDeValidacion_CuandoLaPlantillaEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_turno_de_dia",
            new Dictionary<string, object?> { ["plantilla"] = "   ", ["dia"] = "lunes" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'plantilla' es obligatorio.");
    }
}
