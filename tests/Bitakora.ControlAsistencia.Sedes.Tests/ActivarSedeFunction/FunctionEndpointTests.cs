// Issue #459 (MEF-ADR-0043 paso 4): tests del endpoint HTTP POST sedes/{codigo}:activar.
// CA-ADR-0030 / MEF-ADR-0004: InvalidOperationException -> 409 (CA-3), KeyNotFoundException -> 404
// (CA-5), exito -> 202.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActivarSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-2
    [Fact]
    public async Task ActivarSede_Retorna202_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-3: sede ya activa -> 409 Conflict
    [Fact]
    public async Task ActivarSede_Retorna409_CuandoLaSedeYaEstaActiva()
    {
        var router = new FakeCommandRouter(new InvalidOperationException("La sede ya esta activa"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: sede inexistente -> 404 Not Found
    [Fact]
    public async Task ActivarSede_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task ActivarSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
