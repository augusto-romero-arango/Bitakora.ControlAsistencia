using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.QuitarFranja;

// La invocacion real de quitar_franja sobre una franja existente (MEF-ADR-0048 seccion 6, pieza 3)
// vive en AgregarFranja/AgregarFranjaSmokeTests.cs: quitar_franja deshace ahi lo que agregar_franja
// acaba de agregar en el mismo turno, y separarlas dejaria el ciclo completo duplicado. Aqui quedan
// los caminos de quitar_franja que no dependen de esa franja ya agregada.
public class QuitarFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private static readonly TimeSpan TimeoutPolling = TimeSpan.FromSeconds(30);

    private async Task<JsonDocument?> BuscarFichaPorNombreAsync(string nombre, CancellationToken ct)
    {
        var texto = await programacion.Client.GetStringAsync("/api/programacion/turnos", ct);
        using var documento = JsonDocument.Parse(texto);
        foreach (var turno in documento.RootElement.EnumerateArray())
            if (turno.GetProperty("nombre").GetString() == nombre)
                return JsonDocument.Parse(turno.GetRawText());

        return null;
    }

    // CA-3/CA-4: un turno recien creado no tiene franjas, asi que quitar una hora que no existe
    // llega al 409 real del dominio (FranjaNoExiste), traducido a texto (CA-ADR-0030).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeRechazoDelDominio_CuandoLaFranjaNoExisteEnElTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Turno MCP {Guid.CreateVersion7()}";

        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombreTurno }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await Polling.WaitUntilAsync(() => BuscarFichaPorNombreAsync(nombreTurno, ct), TimeoutPolling);
        ficha.RootElement.GetProperty("franjas").GetArrayLength().Should().Be(0);

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["franja"] = "10:00" },
            cancellationToken: ct);
        resultado.IsError.Should().NotBeTrue("un rechazo de negocio no es un error del protocolo");
        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().StartWith("El dominio rechazo la solicitud:");

        await mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombreTurno }, cancellationToken: ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = "   ", ["franja"] = "15:00" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text.Should().Be("'turno' es obligatorio.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeElMensajeDeValidacion_CuandoLaFranjaEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = "cualquiera", ["franja"] = "   " },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text.Should().Be("'franja' es obligatorio.");
    }

    // CA-3: hora no parseable corta antes de resolver el turno.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeHoraInvalida_CuandoLaFranjaNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = "cualquiera", ["franja"] = "3pm" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'franja' no es una hora valida en formato HH:mm: '3pm'.");
    }

    // CA-3: nombre inexistente -> TurnoNoExiste, resuelto contra el catalogo real de Programacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task QuitarFranja_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = nombre, ["franja"] = "15:00" },
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }
}
