using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaPredeterminadaFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarJornadaPredeterminada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "programacion/preferencias/jornada-predeterminada")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (body, error) = await requestValidator.ValidarAsync<AsignarJornadaPredeterminadaBody>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(new AsignarJornadaPredeterminada(body!.JornadaId), ct);
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
