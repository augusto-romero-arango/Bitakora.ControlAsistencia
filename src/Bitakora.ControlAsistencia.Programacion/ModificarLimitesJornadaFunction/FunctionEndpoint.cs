using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ModificarLimitesJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programacion/jornadas/{id}/limites")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var jornadaId))
            return new BadRequestObjectResult("El id de la jornada no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<ModificarLimitesJornadaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new ModificarLimitesJornada(jornadaId, body!.HorasSemanales, body.TopeDiario,
            body.MinimoDiario, body.DiasDescansoPorSemana);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (RecursoNoEncontradoException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (AggregateException ex)
        {
            return new BadRequestObjectResult(ex.InnerExceptions.Select(e => e.Message));
        }

        return new NoContentResult();
    }
}
