using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.AsignarSedeFranja;

// CA-5: recorre la cadena completa del diseno de turno conversacional -- registrar_sede ->
// crear_turno -> agregar_franja (sin sede) -> asignar_sede_franja (asigna) -> asignar_sede_franja
// (retira) -> retirar_turno. La invocacion real de asignar_sede_franja (MEF-ADR-0048 seccion 6,
// pieza 3) vive aqui, mismo criterio que agregar_franja/quitar_franja (#609).
public class AsignarSedeFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
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

    private static bool SedeDeLaPrimeraFranjaEs(JsonElement ficha, string? codigoEsperado) =>
        ficha.GetProperty("franjas")[0].GetProperty("sedeId").GetString() == codigoEsperado;

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeFranja_AsignaYRetiraLaSedePrearmada_RecorrenElCicloCompletoDeDisenoDeTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        const string nombreSede = "[TEST] Sede MCP Sede Franja";
        var nombreTurno = $"[TEST] SedeFranja MCP {sufijo}";

        await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = codigoSede, ["nombre"] = nombreSede },
            cancellationToken: ct);

        var turnoId = await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var agregadaFranja = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "14:00", ["fin"] = "22:00" },
            cancellationToken: ct);
        agregadaFranja.IsError.Should().NotBeTrue();

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(
                turnoId, ficha => ficha.GetProperty("franjas").GetArrayLength() == 1, ct),
            CatalogoDeTurnos.TimeoutPolling);

        var argumentosAsignar = new Dictionary<string, object?>
        {
            ["turno"] = nombreTurno, ["franja"] = "14:00", ["codigo_sede"] = codigoSede
        };

        // SedeNoExiste llega como texto plano mientras la sede recien registrada no se materializa;
        // Polling reintenta la tool completa hasta el exito (mismo patron que agregar_franja).
        using var ecoAsignado = await Polling.WaitUntilAsync(
            async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "asignar_sede_franja", argumentosAsignar, cancellationToken: ct);
                return JsonDocument.Parse(TextoDe(resultado));
            },
            CatalogoDeTurnos.TimeoutPolling);
        ecoAsignado.RootElement.GetProperty("sede").GetString().Should().Be(nombreSede);

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(turnoId, ficha => SedeDeLaPrimeraFranjaEs(ficha, codigoSede), ct),
            CatalogoDeTurnos.TimeoutPolling);

        var retirada = await mcp.Cliente.CallToolAsync(
            "asignar_sede_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["franja"] = "14:00" },
            cancellationToken: ct);
        retirada.IsError.Should().NotBeTrue();
        using var ecoRetirado = JsonDocument.Parse(TextoDe(retirada));
        ecoRetirado.RootElement.TryGetProperty("sede", out _).Should().BeFalse();

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(turnoId, ficha => SedeDeLaPrimeraFranjaEs(ficha, null), ct),
            CatalogoDeTurnos.TimeoutPolling);

        await RetirarTurnoAsync(nombreTurno, ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeFranja_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_sede_franja",
            new Dictionary<string, object?> { ["turno"] = "   ", ["franja"] = "14:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'turno' es obligatorio.");
    }

    // CA-3: hora no parseable corta antes de resolver el turno.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeFranja_RespondeHoraInvalida_CuandoFranjaNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_sede_franja",
            new Dictionary<string, object?> { ["turno"] = "cualquiera", ["franja"] = "2pm" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'franja' no es una hora valida en formato HH:mm: '2pm'.");
    }

    // CA-3: nombre inexistente -> TurnoNoExiste, resuelto contra el catalogo real de Programacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeFranja_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_sede_franja",
            new Dictionary<string, object?> { ["turno"] = nombre, ["franja"] = "14:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }

    // CA-3: codigo_sede que no esta registrado -> SedeNoExiste, sin llegar a asignar la sede.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AsignarSedeFranja_RespondeSedeNoExiste_CuandoElCodigoDeSedeNoEstaRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] SedeFranja MCP {Guid.CreateVersion7()}";
        var codigoSede = $"TEST-INEXISTENTE-{Guid.CreateVersion7()}";
        await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var resultado = await mcp.Cliente.CallToolAsync(
            "asignar_sede_franja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombreTurno, ["franja"] = "14:00", ["codigo_sede"] = codigoSede
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be($"No existe una sede con el codigo '{codigoSede}'.");

        await RetirarTurnoAsync(nombreTurno, ct);
    }
}
