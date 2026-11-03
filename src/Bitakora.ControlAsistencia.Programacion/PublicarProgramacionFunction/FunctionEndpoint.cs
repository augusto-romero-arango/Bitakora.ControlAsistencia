using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.PublicarProgramacionFunction;

public partial class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    // \z y no $: "$" acepta un "\n" final. Mismo charset URL-safe que la invariante de #387.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoUrlSafe();

    [Function(nameof(PublicarProgramacion))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/colaboradores/{codigo}/borrador:publicar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoUrlSafe().IsMatch(codigo ?? string.Empty))
            return new BadRequestObjectResult(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");

        var (body, error) = await requestValidator.ValidarAsync<PublicarProgramacionBody>(req, ct);
        if (error is not null)
            return error;

        await commandRouter.InvokeAsync(new PublicarProgramacion(codigo!, body!.Desde!.Value, body.Hasta!.Value), ct);

        return new NoContentResult();
    }
}
