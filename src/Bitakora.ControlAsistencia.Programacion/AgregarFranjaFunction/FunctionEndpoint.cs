using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

// POST "{recurso}:{verbo}": la franja es un VO sin identidad propia, direccionado por su hora de
// inicio (MEF-ADR-0043 paso 4).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AgregarFranja")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:agregar-franja")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<AgregarFranjaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AgregarFranja(turnoId, body!.Inicio, body.Fin, body.DiaOffsetFin, body.Sede);

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
