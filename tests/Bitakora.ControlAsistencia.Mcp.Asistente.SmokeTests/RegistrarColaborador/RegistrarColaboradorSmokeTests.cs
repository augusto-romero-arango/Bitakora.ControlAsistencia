using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.Fixtures;
using ModelContextProtocol.Protocol;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SmokeTests.RegistrarColaborador;

public class RegistrarColaboradorSmokeTests(McpFixture mcp)
{
    // Identificacion unica por invocacion: la identidad del stream es "CC-<numero>" y un numero fijo
    // chocaria con 409 en la segunda corrida (deja residuo inofensivo en tenant-smoke). Formato "N" en
    // mayusculas para probar que ArgumentosCrudosMcpMiddleware devuelve el texto exacto, sin la coercion
    // a Guid "D" minusculas de DictionaryStringObjectJsonConverter
    // (Azure/azure-functions-mcp-extension#129).
    private static Dictionary<string, object?> ArgumentosValidos(string fechaInicio = "2026-09-01") => new()
    {
        ["tipo_identificacion"] = "CC",
        ["numero_identificacion"] = Guid.CreateVersion7().ToString("N").ToUpperInvariant(),
        ["primer_nombre"] = "[TEST]",
        ["primer_apellido"] = "MCP",
        ["codigo_colaborador"] = $"TEST-{Guid.CreateVersion7()}",
        ["fecha_inicio"] = fechaInicio
    };

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
        // Si el middleware no restaura el texto original, la tool recibe el Guid en formato "D"
        // minusculas y este assert no encuentra el texto.
        texto.Should().Contain(numeroIdentificacion);
    }

    // El 409 real del dominio (ColaboradorYaRegistrado) debe llegar como TEXTO, nunca como error del
    // protocolo (CA-ADR-0030); el unit test con handler falso no puede probarlo. Autocontenido: registra
    // y repite la MISMA identificacion para no depender del orden de los tests.
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

    // Error path que no toca el dominio: el texto exacto del .resx (FechaInvalida) prueba que los
    // recursos embebidos viajaron en el publish.
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
