using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.ActualizarUbicacionSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static ActualizarUbicacionSedeBody BodyValido() => new("Medellin", "Carrera 50 # 20-30");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-3
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-4
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(BodyValido());
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // Ciudad y Direccion son opcionales, pero un body que no deserializa sigue siendo 400.
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde antes de tocar el comando: sin esta guarda el
    // charset URL-safe que #456 gano solo regiria en el registro (MEF-ADR-0037 seccion 2).
    [Fact]
    public async Task ActualizarUbicacionSede_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var validator = new FakeRequestValidator<ActualizarUbicacionSedeBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "SEDE 001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
