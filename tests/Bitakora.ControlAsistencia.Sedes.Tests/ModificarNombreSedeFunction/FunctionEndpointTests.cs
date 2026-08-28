using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ModificarNombreSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static ModificarNombreSedeBody BodyValido() => new("Sede Renombrada");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-1
    [Fact]
    public async Task ModificarNombreSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4
    [Fact]
    public async Task ModificarNombreSede_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(BodyValido());
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-2
    [Fact]
    public async Task ModificarNombreSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde antes de tocar el comando: sin esta guarda el
    // charset URL-safe que #456 gano solo regiria en el registro (MEF-ADR-0037 seccion 2).
    [Fact]
    public async Task ModificarNombreSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var validator = new FakeRequestValidator<ModificarNombreSedeBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
