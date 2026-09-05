using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;

// Accion de negocio con verbo propio (MEF-ADR-0043 paso 4): la franja no es un sub-recurso
// direccionable por URL -- su clave natural HH:mm contiene ":", fuera del charset URL-safe, y el
// comando lleva payload (la hora, en el body) -- POST "{recurso}:{verbo}". El {id} de ruta se
// valida a mano (MEF-ADR-0037 seccion 2); el body lo valida IRequestValidator.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("QuitarFranja")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:quitar-franja")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<QuitarFranjaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new QuitarFranja(turnoId, body!.Franja);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
