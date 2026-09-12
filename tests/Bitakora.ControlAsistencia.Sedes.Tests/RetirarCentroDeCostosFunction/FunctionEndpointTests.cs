using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarCentroDeCostosFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-3
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna204SinCuerpo_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    // Sede inexistente -> 404 (precondicion de orquestacion). Sin CC vigente NO tiene test de
    // endpoint propio: es un no-op exitoso que sale por el mismo 204 del camino de cambio (#664).
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task RetirarCentroDeCostos_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
