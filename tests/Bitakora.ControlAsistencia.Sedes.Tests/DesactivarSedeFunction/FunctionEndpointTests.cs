using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.DesactivarSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task DesactivarSede_Retorna202_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task DesactivarSede_Retorna409_CuandoLaSedeYaEstaInactiva()
    {
        var router = new FakeCommandRouter(new InvalidOperationException("La sede ya esta inactiva"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task DesactivarSede_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task DesactivarSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
