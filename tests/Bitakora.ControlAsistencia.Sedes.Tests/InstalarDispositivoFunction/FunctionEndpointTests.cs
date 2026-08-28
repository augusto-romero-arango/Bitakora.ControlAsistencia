using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.InstalarDispositivoFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static InstalarDispositivoBody BodyValido() => new("DISP-100");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-1
    [Fact]
    public async Task InstalarDispositivo_Retorna202_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-2: dispositivo ya instalado en esta sede -> 409
    [Fact]
    public async Task InstalarDispositivo_Retorna409_CuandoElDispositivoYaEstaInstalado()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter(
            new InvalidOperationException("El dispositivo ya esta instalado en esta sede"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-5: sede inexistente -> 404
    [Fact]
    public async Task InstalarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter(new KeyNotFoundException("La sede no existe"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // CA-5: DispositivoId vacio o fuera del charset URL-safe -> 400
    [Fact]
    public async Task InstalarDispositivo_Retorna400_CuandoRequestEsInvalido()
    {
        var errorDeValidacion = new BadRequestObjectResult("El body es invalido o esta malformado");
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(error: errorDeValidacion);
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task InstalarDispositivo_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter(
            new KeyNotFoundException("el comando nunca debe despacharse con un codigo invalido"));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
