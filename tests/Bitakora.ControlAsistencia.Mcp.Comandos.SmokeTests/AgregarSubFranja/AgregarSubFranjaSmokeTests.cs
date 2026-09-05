using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.AgregarSubFranja;

// CA-5: recorre la cadena completa de diseno de turno -- crear_turno -> agregar_franja ->
// agregar_subfranja -> quitar_subfranja -> retirar_turno. La invocacion real de quitar_subfranja
// (MEF-ADR-0048 seccion 6, pieza 3) vive aqui, no en un archivo separado -- mismo criterio que
// agregar_franja/quitar_franja (#609).
public class AgregarSubFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    private async Task<object?> EfectoVisibleEnFichaAsync(
        string turnoId, Func<JsonElement, bool> condicion, CancellationToken ct)
    {
        var respuesta = await programacion.Client.GetAsync($"/api/programacion/turnos/{turnoId}", ct);
        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;
        respuesta.EnsureSuccessStatusCode();

        using var documento = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        return condicion(documento.RootElement) ? new object() : null;
    }

    private async Task<string> CrearTurnoYEsperarFichaAsync(string nombre, CancellationToken ct)
    {
        var creado = await mcp.Cliente.CallToolAsync(
            "crear_turno", new Dictionary<string, object?> { ["nombre"] = nombre }, cancellationToken: ct);
        creado.IsError.Should().NotBeTrue();

        using var ficha = await programacion.Client.EsperarFichaAsync(nombre, ct);
        return ficha.RootElement.GetProperty("id").GetString()!;
    }

    private ValueTask<CallToolResult> RetirarTurnoAsync(string nombre, CancellationToken ct) =>
        mcp.Cliente.CallToolAsync(
            "retirar_turno", new Dictionary<string, object?> { ["turno"] = nombre }, cancellationToken: ct);

    private static string TextoDe(CallToolResult resultado) =>
        resultado.Content.OfType<TextContentBlock>().Single().Text;

    private static bool TieneUnaFranja(JsonElement ficha) =>
        ficha.GetProperty("franjas").GetArrayLength() == 1;

    // diaOffsetInicio == 1 lo infiere el dominio a partir de la hora; la tool nunca lo envia.
    private static bool TieneUnDescansoNocturno(JsonElement ficha)
    {
        var descansos = ficha.GetProperty("franjas")[0].GetProperty("descansos");
        return descansos.GetArrayLength() == 1
            && descansos[0].GetProperty("diaOffsetInicio").GetInt32() == 1;
    }

    private static bool NoTieneDescansos(JsonElement ficha) =>
        ficha.GetProperty("franjas")[0].GetProperty("descansos").GetArrayLength() == 0;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_QuitarSubFranja_RecorrenElCicloCompletoDeDisenoDeTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] SubFranja MCP {Guid.CreateVersion7()}";

        var turnoId = await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var agregadaFranja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "22:00", ["fin"] = "06:00" },
            cancellationToken: ct);
        agregadaFranja.IsError.Should().NotBeTrue();

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(turnoId, TieneUnaFranja, ct), CatalogoDeTurnos.TimeoutPolling);

        var argumentosAgregarSubFranja = new Dictionary<string, object?>
        {
            ["turno"] = nombreTurno,
            ["franja"] = "22:00",
            ["tipo"] = "descanso",
            ["inicio"] = "02:00",
            ["fin"] = "02:30"
        };

        var ecoAgregado = await mcp.Cliente.CallToolAsync(
            "agregar_subfranja", argumentosAgregarSubFranja, cancellationToken: ct);
        ecoAgregado.IsError.Should().NotBeTrue();
        using var jsonAgregado = JsonDocument.Parse(TextoDe(ecoAgregado));
        jsonAgregado.RootElement.GetProperty("subFranja").GetString().Should().Be("descanso 02:00-02:30");

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(turnoId, TieneUnDescansoNocturno, ct),
            CatalogoDeTurnos.TimeoutPolling);

        var quitado = await mcp.Cliente.CallToolAsync(
            "quitar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombreTurno,
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00"
            },
            cancellationToken: ct);
        quitado.IsError.Should().NotBeTrue();
        using var jsonQuitado = JsonDocument.Parse(TextoDe(quitado));
        jsonQuitado.RootElement.GetProperty("subFranjaQuitada").GetString().Should().StartWith("descanso 02:00");

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(turnoId, NoTieneDescansos, ct), CatalogoDeTurnos.TimeoutPolling);

        await RetirarTurnoAsync(nombreTurno, ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "   ",
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00",
                ["fin"] = "02:30"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'turno' es obligatorio.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_RespondeTipoDesconocido_CuandoElTipoNoEsDescansoNiExtra()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "cualquiera",
                ["franja"] = "22:00",
                ["tipo"] = "pausa",
                ["inicio"] = "02:00",
                ["fin"] = "02:30"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'pausa' no es un tipo de sub-franja valido. Usa 'descanso' o 'extra'.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_RespondeHoraInvalida_CuandoInicioNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = "cualquiera",
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "2am",
                ["fin"] = "02:30"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'inicio' no es una hora valida en formato HH:mm: '2am'.");
    }

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarSubFranja_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_subfranja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombre,
                ["franja"] = "22:00",
                ["tipo"] = "descanso",
                ["inicio"] = "02:00",
                ["fin"] = "02:30"
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }
}
