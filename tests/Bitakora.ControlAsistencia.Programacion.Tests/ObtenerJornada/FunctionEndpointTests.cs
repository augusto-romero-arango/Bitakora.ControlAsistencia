using System.Reflection;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class FunctionEndpointTests
{
    [Fact]
    public void Run_DeclaraRutaYVerboGet_CuandoSeInspeccionaElTrigger()
    {
        var parametro = typeof(FunctionEndpoint).GetMethod(nameof(FunctionEndpoint.Run))!
            .GetParameters().Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Single(a => a is not null)!;

        parametro.Route.Should().Be("programacion/jornadas/{id}");
        parametro.Methods.Should().BeEquivalentTo(["get"]);
    }

    [Fact]
    public async Task Run_RetornaBadRequest_CuandoElIdNoEsGuid()
    {
        // El IDocumentStore es null! a proposito: debe retornar ANTES de abrir la QuerySession.
        var resultado = await new FunctionEndpoint(null!, null!)
            .Run(new DefaultHttpContext().Request, "no-es-guid", CancellationToken.None);

        resultado.Should().BeOfType<BadRequestObjectResult>();
    }
}
