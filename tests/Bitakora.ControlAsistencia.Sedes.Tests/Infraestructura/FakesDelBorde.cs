using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Sedes.Tests.Infraestructura;

internal sealed class FakeRequestValidator<TComando>(
    TComando? comando = default, IActionResult? error = null) : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(
        HttpRequest req, CancellationToken ct)
    {
        if (error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, error));

        return Task.FromResult<(T?, IActionResult?)>(
            comando is T resultado ? (resultado, null) : (default, null));
    }
}

internal sealed class FakeCommandRouter(Exception? excepcionAlInvocar = null) : ICommandRouter
{
    public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : class =>
        excepcionAlInvocar is null ? Task.CompletedTask : throw excepcionAlInvocar;

    public Task<TResult> InvokeAsync<TCommand, TResult>(
        TCommand command, CancellationToken ct = default)
        where TCommand : class
        => throw new NotImplementedException();
}
