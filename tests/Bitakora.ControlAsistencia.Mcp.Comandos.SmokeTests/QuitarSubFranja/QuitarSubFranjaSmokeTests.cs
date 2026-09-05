using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.QuitarSubFranja;

// La invocacion real de quitar_subfranja sobre una sub-franja existente (MEF-ADR-0048 seccion 6,
// pieza 3) vive en AgregarSubFranja/AgregarSubFranjaSmokeTests.cs: quitar_subfranja deshace ahi lo
// que agregar_subfranja acaba de agregar en el mismo turno. Aqui quedan los caminos que no
// dependen de esa sub-franja ya agregada -- mismo criterio que quitar_franja (#609).
public class QuitarSubFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    // Un turno recien creado no tiene franjas, asi que la llamada alcanza el 409 real del dominio,
    // traducido a texto (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_RespondeRechazoDelDominio_CuandoLaSubFranjaNoExisteEnElTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombreTurno, ct);
        ficha.RootElement.GetProperty("franjas").GetArrayLength().Should().Be(0);

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombreTurno,
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00"
            },
            cancellationToken: ct);
        resultado.IsError.Should().NotBeTrue("un rechazo de negocio no es un error del protocolo");
        TextoDe(resultado).Should().StartWith("El dominio rechazo la solicitud:");

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "   ",
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'turno' es obligatorio.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_RespondeTipoDesconocido_CuandoElTipoNoEsDescansoNiExtra()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "cualquiera",
                ["franja"] = "22:00",
                ["tipo"] = "pausa",
                ["inicio"] = "02:00"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'pausa' no es un tipo de sub-franja valido. Usa 'descanso' o 'extra'.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_RespondeHoraInvalida_CuandoInicioNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "cualquiera",
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "2am"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'inicio' no es una hora valida en formato HH:mm: '2am'.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarSubFranja_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombre,
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }
}
