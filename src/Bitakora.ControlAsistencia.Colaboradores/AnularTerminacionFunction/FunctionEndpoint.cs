using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// POST {recurso}:{verbo} (MEF-ADR-0043 paso 4): la terminacion no es un recurso direccionable ni
// un sub-recurso que se remueva. {codigo} viaja intacto: la comparacion contra la vinculacion
// vigente vive en el aggregate.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("AnularTerminacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion")]
        HttpRequest req,
        string id,
        string codigo,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var comando = new AnularTerminacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            codigo);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
