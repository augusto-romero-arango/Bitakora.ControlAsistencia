// Issue #500: tests del endpoint HTTP DELETE /programacion/turnos/{id}

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Programacion.Tests.RetirarTurnoFunction;

public class FunctionEndpointTests
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000500");

    private static HttpRequest FakeHttpRequest() => new DefaultHttpContext().Request;

    // CA-1
    [Fact]
    public async Task RetirarTurno_Retorna202_CuandoComandoEsValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
    }

    // CA-3: turno ya retirado -> 409
    [Fact]
    public async Task RetirarTurno_Retorna409_CuandoElTurnoYaEstaRetirado()
    {
        var router = new FakeCommandRouter(new InvalidOperationException("El turno ya fue retirado del catalogo"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // CA-2: turno inexistente -> 404
    [Fact]
    public async Task RetirarTurno_Retorna404_CuandoElTurnoNoExiste()
    {
        var router = new FakeCommandRouter(new KeyNotFoundException("No se encontro el turno con el Id especificado"));
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), TurnoId.ToString(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // El {id} de ruta se valida en el borde (MEF-ADR-0037 seccion 2): el comando nunca debe
    // despacharse con un id que no sea un Guid valido.
    [Fact]
    public async Task RetirarTurno_Retorna400_CuandoElIdDeRutaNoEsUnGuidValido()
    {
        var router = new FakeCommandRouter();
        var function = new FunctionEndpoint(router);

        var result = await function.Run(FakeHttpRequest(), "no-es-un-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

// ---- Fake manual - NO NSubstitute ----

internal sealed class FakeCommandRouter(Exception? excepcion = null) : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        if (excepcion is not null) throw excepcion;
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
