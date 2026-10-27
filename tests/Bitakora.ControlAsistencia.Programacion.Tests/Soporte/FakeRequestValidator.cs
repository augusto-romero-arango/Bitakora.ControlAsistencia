using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Soporte;

internal sealed class FakeRequestValidator<TComando>(TComando? comando = default, IActionResult? error = null)
    : IRequestValidator
{
    public Task<(T? Comando, IActionResult? Error)> ValidarAsync<T>(HttpRequest req, CancellationToken ct)
    {
        if (error is not null)
            return Task.FromResult<(T?, IActionResult?)>((default, error));

        if (comando is T resultado)
            return Task.FromResult<(T?, IActionResult?)>((resultado, null));

        return Task.FromResult<(T?, IActionResult?)>((default, null));
    }
}
