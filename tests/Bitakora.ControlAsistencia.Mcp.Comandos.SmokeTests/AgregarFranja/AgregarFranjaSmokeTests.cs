using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.AgregarFranja;

// El ciclo agregar -> quitar va junto (CA-6): quitar_franja deshace en el mismo turno lo que
// agregar_franja acaba de agregar, y retirar_turno es la limpieza. La invocacion real de
// quitar_franja (MEF-ADR-0048 seccion 6, pieza 3) vive aqui, no en QuitarFranja/QuitarFranjaSmokeTests.cs
// -- mismo criterio que CrearTurno/RetirarTurno.
public class AgregarFranjaSmokeTests(McpFixture mcp, ProgramacionApiFixture programacion)
{
    // Sentinela sin estado (igual patron que el poll de borrado de CrearTurnoSmokeTests): la
    // condicion ya evaluo el efecto, no hace falta propagar la ficha completa al llamador.
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

    // Recorre la cadena completa de CA-6: host MCP -> worker -> HttpClients tipados (Programacion +
    // Sedes) -> event stores -> proyecciones. De paso cubre CA-2 (sede prearmada), CA-3 (409 por
    // solape) y CA-4 (eco de quitar_franja compuesto desde la ficha vigente).
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_QuitarFranja_RecorrenElCicloCompletoDeDisenoDeTurno()
    {
        var ct = TestContext.Current.CancellationToken;
        var sufijo = Guid.CreateVersion7();
        var codigoSede = $"TEST-{sufijo}";
        const string nombreSede = "[TEST] Sede MCP Franja";
        var nombreTurno = $"[TEST] Franja MCP {sufijo}";

        await mcp.Cliente.CallToolAsync(
            "registrar_sede",
            new Dictionary<string, object?> { ["codigo"] = codigoSede, ["nombre"] = nombreSede },
            cancellationToken: ct);

        var turnoId = await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var argumentosAgregar = new Dictionary<string, object?>
        {
            ["turno"] = nombreTurno,
            ["inicio"] = "22:00",
            ["fin"] = "06:00",
            ["codigo_sede"] = codigoSede
        };

        // SedeNoExiste llega como texto plano (no JSON) mientras la sede recien registrada no se
        // materializa; JsonDocument.Parse lanza y Polling reintenta la tool completa hasta el exito.
        using var ecoAgregado = await Polling.WaitUntilAsync(
            async () =>
            {
                var resultado = await mcp.Cliente.CallToolAsync(
                    "agregar_franja", argumentosAgregar, cancellationToken: ct);
                return JsonDocument.Parse(TextoDe(resultado));
            },
            CatalogoDeTurnos.TimeoutPolling);
        ecoAgregado.RootElement.GetProperty("franja").GetString().Should().Be($"22:00-06:00, sede: {nombreSede}");

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(
                turnoId,
                ficha => ficha.GetProperty("completo").GetBoolean()
                    && ficha.GetProperty("franjas").GetArrayLength() == 1
                    && ficha.GetProperty("franjas").EnumerateArray().First().GetProperty("sedeId").GetString()
                        == codigoSede,
                ct),
            CatalogoDeTurnos.TimeoutPolling);

        // CA-3: una segunda franja que arranca a la misma hora se solapa con la primera -> 409 del
        // dominio, traducido a texto (CA-ADR-0030), nunca a excepcion del protocolo.
        var repetido = await mcp.Cliente.CallToolAsync("agregar_franja", argumentosAgregar, cancellationToken: ct);
        repetido.IsError.Should().NotBeTrue("un rechazo de negocio no es un error del protocolo");
        TextoDe(repetido).Should().StartWith("El dominio rechazo la solicitud:");

        var quitado = await mcp.Cliente.CallToolAsync(
            "quitar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["franja"] = "22:00" },
            cancellationToken: ct);
        quitado.IsError.Should().NotBeTrue();
        using var ecoQuitado = JsonDocument.Parse(TextoDe(quitado));
        // A diferencia del eco de agregar (linea 84, compuesto con lo enviado), este eco lo compone
        // quitar_franja desde la ficha vigente, donde el dominio ya infirio el offset (CA-ADR-0033) y
        // la notacion unificada de las tools (#612) le agrega el sufijo +1.
        ecoQuitado.RootElement.GetProperty("franjaQuitada").GetString()
            .Should().Be($"22:00-06:00+1, sede: {nombreSede}");

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(
                turnoId,
                ficha => !ficha.GetProperty("completo").GetBoolean()
                    && ficha.GetProperty("franjas").GetArrayLength() == 0,
                ct),
            CatalogoDeTurnos.TimeoutPolling);

        await RetirarTurnoAsync(nombreTurno, ct);
    }

    // CA-1: sin codigo_sede, el body no lleva sede y el eco trae solo el rango.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_AgregaLaFranjaSinSede_CuandoNoLlegaCodigoSede()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Franja MCP {Guid.CreateVersion7()}";
        var turnoId = await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombreTurno, ["inicio"] = "08:00", ["fin"] = "16:00" },
            cancellationToken: ct);
        resultado.IsError.Should().NotBeTrue();
        using var eco = JsonDocument.Parse(TextoDe(resultado));
        eco.RootElement.GetProperty("franja").GetString().Should().Be("08:00-16:00");

        await Polling.WaitUntilAsync(
            () => EfectoVisibleEnFichaAsync(
                turnoId,
                ficha => ficha.GetProperty("completo").GetBoolean()
                    && ficha.GetProperty("franjas").GetArrayLength() == 1,
                ct),
            CatalogoDeTurnos.TimeoutPolling);

        await RetirarTurnoAsync(nombreTurno, ct);
    }

    // CA-2: codigo_sede que no esta registrado -> SedeNoExiste, sin llegar a agregar la franja.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_RespondeSedeNoExiste_CuandoElCodigoDeSedeNoEstaRegistrado()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombreTurno = $"[TEST] Franja MCP {Guid.CreateVersion7()}";
        var codigoSede = $"TEST-INEXISTENTE-{Guid.CreateVersion7()}";
        await CrearTurnoYEsperarFichaAsync(nombreTurno, ct);

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?>
            {
                ["turno"] = nombreTurno,
                ["inicio"] = "08:00",
                ["fin"] = "16:00",
                ["codigo_sede"] = codigoSede
            },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be($"No existe una sede con el codigo '{codigoSede}'.");

        await RetirarTurnoAsync(nombreTurno, ct);
    }

    // Error path que no toca el dominio: campo en blanco corta en el worker (mensaje .resx), prueba
    // que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_RespondeElMensajeDeValidacion_CuandoElTurnoEstaEnBlanco()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = "   ", ["inicio"] = "08:00", ["fin"] = "16:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'turno' es obligatorio.");
    }

    // CA-3: hora no parseable corta antes de resolver el turno.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_RespondeHoraInvalida_CuandoInicioNoTieneFormatoHHmm()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = "cualquiera", ["inicio"] = "8pm", ["fin"] = "16:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().Be("'inicio' no es una hora valida en formato HH:mm: '8pm'.");
    }

    // CA-3: nombre inexistente -> TurnoNoExiste, resuelto contra el catalogo real de Programacion.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task AgregarFranja_RespondeTurnoNoExiste_CuandoElNombreNoEstaEnElCatalogo()
    {
        var ct = TestContext.Current.CancellationToken;
        var nombre = $"[TEST] Turno que no existe {Guid.CreateVersion7()}";

        var resultado = await mcp.Cliente.CallToolAsync(
            "agregar_franja",
            new Dictionary<string, object?> { ["turno"] = nombre, ["inicio"] = "08:00", ["fin"] = "16:00" },
            cancellationToken: ct);

        TextoDe(resultado).Should().StartWith($"No existe un turno con el nombre '{nombre}'.");
    }
}
