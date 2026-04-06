using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using CrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// HU-4: Endpoint HTTP POST para crear un turno de trabajo
// ADR-0008: [Function(nameof(CrearTurno))] como convencion de nombrado
// Flujo: validar request -> despachar comando -> retornar 202 o error
// ADR-0007: InvalidOperationException -> 409 Conflict
//           AggregateException (del factory) -> 400 Bad Request con mensajes
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function(nameof(FunctionEndpoint))]
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

        return new AcceptedResult();
    }
}
