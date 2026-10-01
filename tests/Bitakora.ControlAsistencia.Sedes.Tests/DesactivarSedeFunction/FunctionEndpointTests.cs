using AwesomeAssertions;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;
using Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction.CommandHandler;
using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Sedes.Tests.DesactivarSedeFunction;

public class FunctionEndpointTests
{
    private const string Codigo = "SEDE-001";

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task DesactivarSede_Retorna204SinCuerpo_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        result.Should().NotBeAssignableTo<ObjectResult>();
    }

    [Fact]
    public async Task DesactivarSede_Retorna409_CuandoLaSedeYaEstaInactiva()
    {
        var router = new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(DesactivarSedeCommandHandler.Mensajes.SedeYaInactiva));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task DesactivarSede_Retorna404_CuandoSedeNoExiste()
    {
        var router = new FakeCommandRouter(new RecursoNoEncontradoException(DesactivarSedeCommandHandler.Mensajes.SedeNoEncontrada));
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
            new Exception());
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "SEDE:001", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DesactivarSede_PropagaInvalidOperationException_CuandoFallaLaInfraestructura()
    {
        var function = new FunctionEndpoint(new FakeCommandRouter(new InvalidOperationException()));

        var act = () => function.Run(FakeHttpRequest(), Codigo, CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }
}
