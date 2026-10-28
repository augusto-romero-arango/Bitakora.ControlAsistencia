using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Soporte;

internal sealed class FakeCommandRouter(
    Exception? excepcion = null, ArgumentException[]? erroresAggregateException = null) : ICommandRouter
{
    public bool Invocado { get; private set; }

    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class
    {
        Invocado = true;
        if (excepcion is not null) throw excepcion;
        if (erroresAggregateException is not null) throw new AggregateException(erroresAggregateException);
        return Task.CompletedTask;
    }

    public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        throw new NotImplementedException();
}
