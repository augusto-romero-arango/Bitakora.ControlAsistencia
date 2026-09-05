using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

// 201 Created, nunca AcceptedResult: ICommandRouter.InvokeAsync es inline y la transaccion
// (UnitOfWorkMiddleware + AutoApplyTransactions) confirma antes de responder, asi que el objeto ya
// quedo persistido en este mismo POST. Los endpoints del BC que aun devuelven 202 se corrigen en
// #640; no alinear este a ellos.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CrearPlantillaSemanal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/plantillas-semanales")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<CrearPlantillaSemanal>(req, ct);
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

        // URI canonica de lectura: el GET que la sirve llega en #625 -- hasta entonces responde 404.
        return new CreatedResult($"/api/programacion/plantillas-semanales/{comando!.PlantillaId}", null);
    }
}
