using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction;

public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("MarcarDescansoAcumuladoDeJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programacion/jornadas/{id}/descanso-acumulado")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var jornadaId))
            return new BadRequestObjectResult("El id de la jornada no es un Guid valido");

        var comando = new MarcarDescansoAcumuladoDeJornada(jornadaId);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (RecursoNoEncontradoException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (ReglaDeNegocioDeclinadaException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }
        catch (AggregateException ex)
        {
            return new BadRequestObjectResult(ex.InnerExceptions.Select(e => e.Message));
        }

        return new NoContentResult();
    }
}
