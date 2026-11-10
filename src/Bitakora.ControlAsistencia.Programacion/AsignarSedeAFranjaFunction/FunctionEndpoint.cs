using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// POST "{recurso}:{verbo}" (MEF-ADR-0043 paso 4): la sede no es direccionable por URL (la franja tiene
// clave HH:mm y ":" no es URL-safe) y la misma accion cubre asignar y retirar.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarSedeAFranja")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:asignar-sede-franja")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<AsignarSedeAFranjaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarSedeAFranja(turnoId, body!.Franja, body.Sede);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
