using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction;

// Remocion veraz y SIN body (MEF-ADR-0043 paso 3), asi que el {id} de ruta es lo unico que
// validar (MEF-ADR-0037 seccion 2). Una plantilla ya retirada responde 204 igual que una recien
// retirada (DELETE idempotente, RFC 9110 seccion 9.2.2): nunca 409 ni AcceptedResult.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarPlantillaSemanal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "programacion/plantillas-semanales/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var plantillaId))
            return new BadRequestObjectResult("El id de la plantilla no es un Guid valido");

        try
        {
            await commandRouter.InvokeAsync(new RetirarPlantillaSemanal(plantillaId), ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new NoContentResult();
    }
}
