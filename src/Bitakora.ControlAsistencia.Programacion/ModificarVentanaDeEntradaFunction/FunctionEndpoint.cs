using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ModificarVentanaDeEntrada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programacion/turnos/{id}/ventana-entrada")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<ModificarVentanaDeEntradaBody>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(new ModificarVentanaDeEntrada(turnoId, body!.Amplitud), ct);
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
