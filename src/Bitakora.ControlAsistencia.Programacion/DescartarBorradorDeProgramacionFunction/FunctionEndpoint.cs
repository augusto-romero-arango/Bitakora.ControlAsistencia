using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction;

public partial class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    // \z y no $: "$" acepta un "\n" final.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoUrlSafe();

    [Function(nameof(DescartarBorradorDeProgramacion))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/colaboradores/{codigo}/borrador:descartar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoUrlSafe().IsMatch(codigo ?? string.Empty))
            return new BadRequestObjectResult(Mensajes.CodigoNoEsUrlSafe);

        var (body, error) = await requestValidator.ValidarAsync<DescartarBorradorDeProgramacionBody>(req, ct);
        if (error is not null)
            return error;

        await commandRouter.InvokeAsync(new DescartarBorradorDeProgramacion(codigo!, body!.Desde!.Value, body.Hasta!.Value), ct);

        return new NoContentResult();
    }
}
