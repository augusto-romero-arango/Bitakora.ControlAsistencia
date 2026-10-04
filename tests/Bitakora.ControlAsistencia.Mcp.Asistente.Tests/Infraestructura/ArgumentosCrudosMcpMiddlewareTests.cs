using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ArgumentosCrudosMcpMiddlewareTests
{
    [Fact]
    public void RestaurarTextoOriginal_DevuelveElTextoExacto_CuandoLaExtensionCoercionoUnaFecha()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["fecha_inicio"] = DateTimeOffset.Parse("2026-09-01T00:00:00+00:00") }
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new { fecha_inicio = "2026-09-01" }
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["fecha_inicio"].Should().Be("2026-09-01");
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElTextoExacto_CuandoLaExtensionCoercionoUnGuid()
    {
        const string guidOriginalFormatoN = "3FA85F6457174562B3FC2C963F66AFA6";
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["identificador"] = Guid.Parse(guidOriginalFormatoN) }
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new { identificador = guidOriginalFormatoN }
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["identificador"].Should().Be(guidOriginalFormatoN);
    }

    [Fact]
    public void RestaurarTextoOriginal_DejaIntactosLosEscalaresYCompuestos_CuandoNingunoEsUnaHojaDeTexto()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object>
            {
                ["cantidad"] = 5,
                ["activo"] = true,
                ["comentario"] = null!,
                ["opciones"] = new Dictionary<string, object> { ["clave"] = "valor" },
                ["etiquetas"] = new[] { "a", "b" }
            }
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new
            {
                cantidad = 5,
                activo = true,
                comentario = (string?)null,
                opciones = new { clave = "valor" },
                etiquetas = new[] { "a", "b" }
            }
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments.Should().BeEquivalentTo(bindeado.Arguments);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElMismoContexto_CuandoArgumentsEsNulo()
    {
        var bindeado = new ToolInvocationContext { Name = "cualquier_tool", Arguments = null };

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(
            bindeado, """{"name":"cualquier_tool"}""");

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElMismoContexto_CuandoElJsonNoTraeArguments()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["fecha_inicio"] = DateTimeOffset.UtcNow }
        };

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(
            bindeado, """{"name":"cualquier_tool"}""");

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_DevuelveElMismoContexto_CuandoArgumentsDelJsonNoEsUnObjeto()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["fecha_inicio"] = DateTimeOffset.UtcNow }
        };

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(
            bindeado, """{"name":"cualquier_tool","arguments":null}""");

        restaurado.Should().BeSameAs(bindeado);
    }

    [Fact]
    public void RestaurarTextoOriginal_ResuelveLaClaveSinDistinguirMayusculas_CuandoElJsonUsaOtroCasing()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["Fecha_Inicio"] = DateTimeOffset.Parse("2026-09-01T00:00:00+00:00") }
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new { fecha_inicio = "2026-09-01" }
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments!["FECHA_INICIO"].Should().Be("2026-09-01");
    }

    [Fact]
    public void RestaurarTextoOriginal_ConservaNombreSesionYTransporte_CuandoRestauraArgumentos()
    {
        var transporte = new HttpTransport("http-streamable");
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["fecha_inicio"] = DateTimeOffset.Parse("2026-09-01T00:00:00+00:00") },
            SessionId = "sesion-1",
            Transport = transporte
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new { fecha_inicio = "2026-09-01" },
            sessionid = "sesion-1"
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Name.Should().Be(bindeado.Name);
        restaurado.SessionId.Should().Be(bindeado.SessionId);
        restaurado.Transport.Should().BeSameAs(transporte);
    }

    [Fact]
    public void RestaurarTextoOriginal_NoAgregaClavesNuevas_CuandoElJsonTraeUnaClaveQueElBindeadoNoTiene()
    {
        var bindeado = new ToolInvocationContext
        {
            Name = "cualquier_tool",
            Arguments = new Dictionary<string, object> { ["fecha_inicio"] = DateTimeOffset.Parse("2026-09-01T00:00:00+00:00") }
        };
        var jsonCrudo = JsonSerializer.Serialize(new
        {
            name = "cualquier_tool",
            arguments = new { fecha_inicio = "2026-09-01", clave_extra = "no deberia agregarse" }
        });

        var restaurado = ArgumentosCrudosMcpMiddleware.RestaurarTextoOriginal(bindeado, jsonCrudo);

        restaurado.Arguments.Should().ContainSingle().Which.Key.Should().Be("fecha_inicio");
    }
}
