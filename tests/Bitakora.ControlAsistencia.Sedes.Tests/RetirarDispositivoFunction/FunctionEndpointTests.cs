using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;
using Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Sedes.Tests.RetirarDispositivoFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";
    private const string DispositivoId = "DISP-100";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-3
    [Fact]
    public async Task RetirarDispositivo_Retorna204SinCuerpo_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    // Sede inexistente -> 404 (precondicion de orquestacion). Dispositivo no instalado NO tiene test
    // de endpoint propio: es un no-op exitoso que sale por el mismo 204 del camino de cambio (#664).
    [Fact]
    public async Task RetirarDispositivo_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(RetirarDispositivoCommandHandler.Mensajes.SedeNoEncontrada));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {codigo} de ruta se rechaza en el borde: el FakeCommandRouter lanzaria si el comando
    // llegara a despacharse con un codigo invalido.
    [Fact]
    public async Task RetirarDispositivo_Retorna400_CuandoCodigoDeRutaNoEsUrlSafe()
    {
        var router = new FakeCommandRouter(
            new Exception());
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", DispositivoId, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RetirarDispositivo_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(new FakeCommandRouter(new InvalidOperationException()));

        var act = () => function.Run(FakeHttpRequest(), Codigo, DispositivoId, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}
