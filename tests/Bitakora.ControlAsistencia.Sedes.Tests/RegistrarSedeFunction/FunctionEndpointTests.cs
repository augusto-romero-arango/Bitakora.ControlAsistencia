using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RegistrarSedeFunction;

public class FunctionEndpointTests
{
    private static RegistrarSede ComandoValido() =>
        new("SEDE-001", "Sede Principal", "Bogota", "Calle 100 # 10-20");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task RegistrarSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<RegistrarSede>(ComandoValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task RegistrarSede_Retorna409_CuandoCodigoYaExiste()
    {
        var validator = new FakeRequestValidator<RegistrarSede>(ComandoValido());
        var router = new FakeCommandRouter(new InvalidOperationException("La sede ya existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task RegistrarSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<RegistrarSede>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
