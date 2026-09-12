using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// ADR-0007: InvalidOperationException -> 409 Conflict
//           AggregateException (del factory) -> 400 Bad Request con mensajes
// CA-ADR-0035: exito -> 201 Created con Location a la ficha del turno (ObtenerFichaTurno); la
// transaccion confirma antes de responder, nunca 202.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CrearTurno")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<CrearTurno>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(comando!, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }
        catch (AggregateException ex)
        {
            return new BadRequestObjectResult(
                ex.InnerExceptions.Select(e => e.Message));
        }

        return new CreatedResult($"/api/programacion/turnos/{comando!.TurnoId}", null);
    }
}
