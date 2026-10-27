using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerCuadroDeTurnos;

// El guard retorna ANTES de abrir la QuerySession: el IDocumentStore es null! a proposito.
public class FunctionEndpointTests
{
    [Theory]
    [InlineData("no-es-un-guid")]
    [InlineData("123")]
    public async Task ObtenerCuadroDeTurnos_Retorna400_CuandoElIdNoEsGuid(string id)
    {
        var context = new DefaultHttpContext();

        var resultado = await new FunctionEndpoint(null!, null!).Run(context.Request, id, CancellationToken.None);

        resultado.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
