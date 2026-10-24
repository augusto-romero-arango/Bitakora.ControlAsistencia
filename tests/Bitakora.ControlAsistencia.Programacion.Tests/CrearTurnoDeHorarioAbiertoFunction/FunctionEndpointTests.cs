using AwesomeAssertions;
using HorasYMinutosSolicitadas = Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.HorasYMinutosSolicitadas;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

public class FunctionEndpointTests
{
    private static CrearTurnoDeHorarioAbierto ComandoValido() =>
        new(Guid.NewGuid(), "Turno Abierto", new HorasYMinutosSolicitadas(6, 0));

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Retorna201ConLocation_CuandoComandoEsValido()
    {
        var comando = ComandoValido();
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurnoDeHorarioAbierto>(comando), new FakeCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeAssignableTo<IStatusCodeActionResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
        result.Should().BeAssignableTo<CreatedResult>()
            .Which.Location.Should().Be($"/api/programacion/turnos/{comando.TurnoId}");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Retorna409_CuandoTurnoYaExiste()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurnoDeHorarioAbierto>(ComandoValido()),
            new FakeCommandRouter(new RecursoYaExisteException(
                CrearTurnoDeHorarioAbiertoCommandHandler.Mensajes.TurnoYaExiste)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Retorna409_CuandoNombreEstaDuplicado()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurnoDeHorarioAbierto>(ComandoValido()),
            new FakeCommandRouter(new ReglaDeNegocioDeclinadaException(
                CrearTurnoDeHorarioAbiertoCommandHandler.Mensajes.NombreDuplicado)));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_Retorna400_CuandoLaDuracionEsInvalida()
    {
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurnoDeHorarioAbierto>(ComandoValido()),
            new FakeCommandRouter(new AggregateException(new ArgumentException("duracion invalida"))));

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_RetornaElErrorDelValidador_CuandoElCuerpoEsInvalido()
    {
        var error = new BadRequestObjectResult("invalido");
        var function = new FunctionEndpoint(
            new FakeRequestValidator<CrearTurnoDeHorarioAbierto>(error: error), new FakeCommandRouter());

        var result = await function.Run(FakeHttpRequest(), CancellationToken.None);

        result.Should().BeSameAs(error);
    }
}
