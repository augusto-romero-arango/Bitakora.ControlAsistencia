using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Sedes.Tests.InstalarDispositivoFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static InstalarDispositivoBody BodyValido() => new("DISP-100");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-1
    [Fact]
    public async Task InstalarDispositivo_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.As<CreatedResult>().Location.Should().Be($"/api/sedes/fichas/{Codigo}");
    }

    // CA-2: dispositivo ya instalado en esta sede -> 409
    [Fact]
    public async Task InstalarDispositivo_Retorna409_CuandoElDispositivoYaEstaInstalado()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter(
            new ReglaDeNegocioDeclinadaException(InstalarDispositivoCommandHandler.Mensajes.DispositivoYaInstalado));
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // Sede inexistente -> 404 (precondicion de orquestacion, no un CA propio del issue)
    [Fact]
    public async Task InstalarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var validator = new FakeRequestValidator<InstalarDispositivoBody>(BodyValido());
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(InstalarDispositivoCommandHandler.Mensajes.SedeNoEncontrada));
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
            new Exception());
        var function = new FunctionEndpoint(validator, router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task InstalarDispositivo_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<InstalarDispositivoBody>(BodyValido()),
            new FakeCommandRouter(new InvalidOperationException()));

        var act = () => function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}
