using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.DesactivarJornadaFunction;

public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("DesactivarJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/jornadas/{id}:desactivar")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var jornadaId))
            return new BadRequestObjectResult("El id de la jornada no es un Guid valido");

        try
        {
            await commandRouter.InvokeAsync(new DesactivarJornada(jornadaId), ct);
        }
        catch (RecursoNoEncontradoException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (ReglaDeNegocioDeclinadaException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }

        return new NoContentResult();
    }
}
