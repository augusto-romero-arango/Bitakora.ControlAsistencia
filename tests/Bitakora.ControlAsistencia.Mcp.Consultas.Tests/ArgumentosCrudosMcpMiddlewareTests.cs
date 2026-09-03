using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// Nucleo testable de ArgumentosCrudosMcpMiddleware (issue #586): RestaurarTextoOriginal opera
// sobre un ToolInvocationContext ya bindeado (con los strings coercionados por
// DictionaryStringObjectJsonConverter, Azure/azure-functions-mcp-extension#129) y el JSON crudo de
// la invocacion tal como llego en context.BindingContext.BindingData. FunctionContext no es
// instanciable en un unit test (mismo limite que IdentidadTenantMcpMiddlewareTests, MEF-ADR-0048
// seccion 1), pero ToolInvocationContext si lo es: es una clase publica con propiedades init.
public class ArgumentosCrudosMcpMiddlewareTests
{
    // Replica el sobre {"name":..., "arguments":{...}, "sessionid":...} que arma el host
    // (McpToolTriggerBinding.cs) -- solo "arguments" le interesa al nucleo.
    private static string ConstruirJsonCrudo(object? argumentos, string nombreTool = "consultar_programacion") =>
        JsonSerializer.Serialize(new { name = nombreTool, arguments = argumentos, sessionid = "sesion-1" });

    private static ToolInvocationContext CrearBindeado(Dictionary<string, object?> argumentos)
    {
        // El diccionario real de la extension anota TValue como object, pero el converter si
        // produce null para JsonTokenType.Null (ReadValue): el null! de abajo replica eso mismo.
        var copia = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (clave, valor) in argumentos)
            copia[clave] = valor!;

        return new ToolInvocationContext { Name = "consultar_programacion", SessionId = "sesion-1", Arguments = copia };
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElTextoExacto_CuandoElArgumentoTieneFormaDeFecha()
    {
        var bindeado = CrearBindeado(new()
        {
            // Valor ya coercionado por la extension: Utf8JsonReader.TryGetDateTimeOffset lo capturo
            // como DateTimeOffset antes de que McpInputConversionHelper lo convirtiera a texto
            // reformateado -- aqui se simula ya como DateTimeOffset, el punto de entrada del bug.
            ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        var jsonCrudo = ConstruirJsonCrudo(new { desde = "2026-09-01" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["desde"].Should().Be("2026-09-01");
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElTextoExacto_CuandoElArgumentoTieneFormaDeGuid()
    {
        const string guidOriginal = "F47AC10B58CC4372A5670E02B2C3D479";
        var bindeado = CrearBindeado(new()
        {
            // Guid.TryParse ya lo convirtio: sobrevive en formato "D" minusculas, sin el casing ni
            // el formato "N" con el que lo envio el cliente.
            ["identificacion"] = Guid.Parse(guidOriginal),
        });
        var jsonCrudo = ConstruirJsonCrudo(new { identificacion = guidOriginal });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["identificacion"].Should().Be(guidOriginal);
    }

    [Fact]
    public void RestaurarTextoOriginal_DejaIntactosLosValoresEscalares_CuandoElJsonTraeNumeroBooleanoYNulo()
    {
        var bindeado = CrearBindeado(new()
        {
            ["cantidad"] = 5,
            ["activo"] = true,
            ["nota"] = null,
        });
        var jsonCrudo = ConstruirJsonCrudo(new { cantidad = 5, activo = true, nota = (string?)null });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["cantidad"].Should().Be(5);
        restaurado.Arguments!["activo"].Should().Be(true);
        restaurado.Arguments!["nota"].Should().BeNull();
    }

    [Fact]
    public void RestaurarTextoOriginal_DejaIntactosLosValoresCompuestos_CuandoElJsonTraeObjetoYArreglo()
    {
        var metadataBindeada = new Dictionary<string, object> { ["clave"] = "valor" };
        var listaBindeada = new List<object> { 1, 2, 3 };
        var bindeado = CrearBindeado(new()
        {
            ["metadata"] = metadataBindeada,
            ["lista"] = listaBindeada,
        });
        var jsonCrudo = ConstruirJsonCrudo(new { metadata = new { clave = "valor" }, lista = new[] { 1, 2, 3 } });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        // No se tocan (mismo objeto, no una copia): la restauracion solo reemplaza hojas string.
        restaurado.Arguments!["metadata"].Should().BeSameAs(metadataBindeada);
        restaurado.Arguments!["lista"].Should().BeSameAs(listaBindeada);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElContextoOriginal_CuandoElJsonNoTraePropiedadArguments()
    {
        var bindeado = CrearBindeado(new()
        {
            ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        var jsonCrudo = JsonSerializer.Serialize(new { name = "consultar_programacion", sessionid = "sesion-1" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElContextoOriginal_CuandoLaPropiedadArgumentsEsNulaEnElJson()
    {
        var bindeado = CrearBindeado(new()
        {
            ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        var jsonCrudo = ConstruirJsonCrudo(argumentos: null);

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElContextoOriginal_CuandoElBindeadoNoTraeArguments()
    {
        var bindeado = new ToolInvocationContext { Name = "consultar_programacion", Arguments = null };
        var jsonCrudo = ConstruirJsonCrudo(new { desde = "2026-09-01" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_ResuelveElNombreDelArgumento_SinDistinguirMayusculas()
    {
        var bindeado = CrearBindeado(new()
        {
            ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        // El diccionario bindeado (upstream, StringComparer.OrdinalIgnoreCase) no distingue casing;
        // el JSON crudo puede traer el mismo nombre con otro casing sin que eso rompa el match.
        var jsonCrudo = ConstruirJsonCrudo(new { Desde = "2026-09-01" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!.Should().ContainKey("desde");
        restaurado.Arguments!["desde"].Should().Be("2026-09-01");
    }

    [Fact]
    public void RestaurarTextoOriginal_ConservaNombreSesionYTransporte_CuandoReemplazaLosArgumentos()
    {
        var transporte = new HttpTransport("http-streamable");
        var bindeado = new ToolInvocationContext
        {
            Name = "consultar_programacion",
            SessionId = "sesion-1",
            Transport = transporte,
            Arguments = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            },
        };
        var jsonCrudo = ConstruirJsonCrudo(new { desde = "2026-09-01" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        // La copia solo reemplaza Arguments: el SessionId lo usa el host para la afinidad de sesion
        // y el Transport es de donde IdentidadTenantMcpMiddleware lee el Authorization -- perderlos
        // no rompe ninguna asercion sobre el texto restaurado, pero si la tool call en runtime.
        restaurado.Name.Should().Be("consultar_programacion");
        restaurado.SessionId.Should().Be("sesion-1");
        restaurado.Transport.Should().BeSameAs(transporte);
    }

    [Fact]
    public void RestaurarTextoOriginal_NoAgregaElArgumento_CuandoLaClaveDelJsonNoEstaEnElBindeado()
    {
        var bindeado = CrearBindeado(new()
        {
            ["desde"] = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
        });
        // El diccionario bindeado es la fuente autorizada de que argumentos existen; el JSON crudo
        // solo aporta el texto original de los que ya estan.
        var jsonCrudo = ConstruirJsonCrudo(new { desde = "2026-09-01", ajeno = "no declarado" });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!.Should().ContainSingle().Which.Key.Should().Be("desde");
    }
}
