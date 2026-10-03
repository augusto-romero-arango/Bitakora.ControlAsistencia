using System.Text.RegularExpressions;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.CancelarAusenciaFunction;

public partial class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    // \z y no $: "$" acepta un "\n" final. Mismo charset URL-safe que la invariante de #387.
    [GeneratedRegex(@"\A[A-Za-z0-9._~-]+\z")]
    private static partial Regex CodigoUrlSafe();

    [Function(nameof(CancelarAusencia))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/colaboradores/{codigo}/ausencias/{id}:cancelar")]
        HttpRequest req,
        string codigo,
        string id,
        CancellationToken ct)
    {
        if (!CodigoUrlSafe().IsMatch(codigo ?? string.Empty))
            return new BadRequestObjectResult(
                "El codigo del colaborador solo admite letras sin tilde, digitos y los caracteres - . _ ~");

        if (!Guid.TryParse(id, out var ausenciaId))
            return new BadRequestObjectResult("El id de la ausencia debe ser un Guid");

        var (body, error) = await requestValidator.ValidarAsync<CancelarAusenciaBody>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(new CancelarAusencia(codigo!, ausenciaId, body!.Fechas!), ct);
        }
        catch (RecursoNoEncontradoException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new NoContentResult();
    }
}
