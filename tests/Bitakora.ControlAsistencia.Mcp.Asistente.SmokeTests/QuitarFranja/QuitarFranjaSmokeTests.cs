using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.QuitarFranja;

// La invocacion real de quitar_franja sobre una franja existente (MEF-ADR-0048 seccion 6, pieza 3)
// vive en AgregarFranjaSmokeTests; aqui quedan los caminos que no la necesitan.
public class QuitarFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // Un turno recien creado no tiene franjas: quitar una hora llega al 409 real del dominio
    // (FranjaNoExiste), traducido a texto (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeRechazoDelDominio_CuandoLaFranjaNoExisteEnElTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        var turnoId = ficha.RootElement.GetProperty("id").GetString()!;
        ficha.RootElement.GetProperty("franjas").GetArrayLength().Should().Be(0);

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoId, ["franja"] = "10:00" },
            cancellationToken: ct);
        resultado.IsError.Should().NotBeTrue("un rechazo de negocio no es un error del protocolo");
        TextoDe(resultado).Should().StartWith("El dominio rechazo la solicitud:");

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno_id"] = turnoId }, cancellationToken: ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeElMensajeDeValidacion_CuandoElTurnoIdEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno_id"] = "   ", ["franja"] = "15:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().StartWith("turno_id debe ser el id del turno");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeElMensajeDeValidacion_CuandoLaFranjaEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno_id"] = Guid.NewGuid().ToString(), ["franja"] = "   " },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'franja' es obligatorio.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeHoraInvalida_CuandoLaFranjaNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno_id"] = Guid.NewGuid().ToString(), ["franja"] = "3pm" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'franja' no es una hora valida en formato HH:mm: '3pm'.");
    }

    // 404 del detalle real de Programacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeTurnoNoExiste_CuandoElTurnoIdNoExiste()
    {
        var ct = TestContext.Current.CancellationToken;
        var turnoInexistente = Guid.CreateVersion7().ToString();

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno_id"] = turnoInexistente, ["franja"] = "15:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Contain(turnoInexistente).And.Contain("buscar_turnos");
    }
}
