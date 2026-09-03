using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.SmokeTests.RegistrarColaborador;

public class RegistrarColaboradorSmokeTests(McpFixture mcp)
{
    // Identificacion y codigo unicos por invocacion: la identidad del stream es
    // Identificacion.ToString() ("CC-<numero>"), asi que reusar un numero fijo chocaria con 409 en
    // la segunda corrida. Formato "N" en mayusculas (issue #586): el assert de numero_identificacion
    // de abajo confirma que ArgumentosCrudosMcpMiddleware devuelve el texto exacto enviado, sin la
    // coercion a Guid formato "D" minusculas que aplica DictionaryStringObjectJsonConverter
    // (Azure/azure-functions-mcp-extension#129). Residuo inofensivo en tenant-smoke (precedente #547).
    private static Dictionary<string, object?> ArgumentosValidos(string fechaInicio = "2026-09-01") => new()
    {
        ["tipo_identificacion"] = "CC",
        ["numero_identificacion"] = Guid.CreateVersion7().ToString("N").ToUpperInvariant(),
        ["primer_nombre"] = "[TEST]",
        ["primer_apellido"] = "MCP",
        ["codigo_colaborador"] = $"TEST-{Guid.CreateVersion7()}",
        ["fecha_inicio"] = fechaInicio
    };

    // Recorre la cadena completa: host MCP -> worker -> HttpClient tipado -> Function App de
    // Colaboradores -> event store.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_RegistraElColaborador_CuandoLosDatosSonValidos()
    {
        var ct = TestContext.Current.CancellationToken;
        var argumentos = ArgumentosValidos();
        var codigo = (string)argumentos["codigo_colaborador"]!;
        var numeroIdentificacion = (string)argumentos["numero_identificacion"]!;

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_colaborador", argumentos, cancellationToken: ct);

        resultado.IsError.Should().NotBeTrue();
        var texto = resultado.Content.OfType<TextContentBlock>().Single().Text;

        texto.Should().Contain(codigo);
        // CA-1 (issue #586): el eco trae numero_identificacion EXACTAMENTE como se envio (GUID "N"
        // mayusculas) -- si el middleware no restaura el texto original, la tool recibe el Guid ya
        // coercionado a formato "D" minusculas y este assert no encuentra el texto.
        texto.Should().Contain(numeroIdentificacion);
    }

    // CA-2 end-to-end: el 409 del dominio (ColaboradorYaRegistrado) tiene que llegar al asistente
    // como TEXTO traducido, nunca como error del protocolo -- es la decision de CA-ADR-0030, y el
    // unit test con handler falso no puede probar que el dominio real responda ese status ni ese
    // texto exacto. Autocontenido (registra y repite la MISMA identificacion) para no depender del
    // orden de los tests ni sembrar un colaborador extra.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_DevuelveElRechazoComoTexto_CuandoLaIdentificacionYaEstaRegistrada()
    {
        var ct = TestContext.Current.CancellationToken;
        var argumentos = ArgumentosValidos();

        await mcp.Cliente.CallToolAsync("registrar_colaborador", argumentos, cancellationToken: ct);
        var reintento = await mcp.Cliente.CallToolAsync(
            "registrar_colaborador", argumentos, cancellationToken: ct);

        reintento.IsError.Should().NotBeTrue("un rechazo de negocio no es un error del protocolo");
        reintento.Content.OfType<TextContentBlock>().Single().Text
            .Should().Contain("Ya existe un colaborador registrado con esa identificacion");
    }

    // Error path que no toca el dominio: la validacion de formato de fecha_inicio corta en el
    // worker y responde el mensaje del .resx FechaInvalida en produccion. Afirmar el texto exacto
    // prueba que los recursos embebidos viajaron en el publish.
    [Fact]
    [Trait("Category", "Smoke")]
    public async Task RegistrarColaborador_RespondeElMensajeDeValidacion_CuandoFechaInicioTieneFormatoInvalido()
    {
        var ct = TestContext.Current.CancellationToken;

        var resultado = await mcp.Cliente.CallToolAsync(
            "registrar_colaborador",
            ArgumentosValidos(fechaInicio: "2026-99-99"),
            cancellationToken: ct);

        resultado.Content.OfType<TextContentBlock>().Single().Text
            .Should().Be("'fecha_inicio' debe tener formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
