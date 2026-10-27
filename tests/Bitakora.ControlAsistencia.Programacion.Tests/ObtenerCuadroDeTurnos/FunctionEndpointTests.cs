using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroDeTurnos;

// El guard retorna ANTES de abrir la QuerySession: el IDocumentStore es null! a proposito.
public class FunctionEndpointTests
{
    private static async Task<int?> EjecutarAsync(string id)
    {
        var context = new DefaultHttpContext();
        var resultado = await new FunctionEndpoint(null!, null!).Run(context.Request, id, CancellationToken.None);
        return resultado.Should().BeAssignableTo<IStatusCodeActionResult>().Subject.StatusCode;
    }

    [Fact]
    public async Task ObtenerCuadroDeTurnos_Retorna400_CuandoElIdNoEsGuid()
    {
        (await EjecutarAsync("no-es-un-guid")).Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ObtenerCuadroDeTurnos_Retorna400_CuandoElIdEsNumerico()
    {
        (await EjecutarAsync("123")).Should().Be(StatusCodes.Status400BadRequest);
    }
}
