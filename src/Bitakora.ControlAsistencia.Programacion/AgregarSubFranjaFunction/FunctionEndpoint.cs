using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// POST "{recurso}:{verbo}": la sub-franja es un VO sin identidad propia, direccionada por franja + hora
// de inicio (MEF-ADR-0043 paso 4).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AgregarSubFranja")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:agregar-subfranja")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<AgregarSubFranjaBody>(req, ct);
        if (error is not null)
            return error;

        // Enum.Parse lanzaria fuera del try (500): esta guarda reemite el 400 del validator para que el
        // borde no dependa de que el validator este registrado.
        if (!Enum.TryParse<TipoSubFranja>(body!.Tipo, ignoreCase: true, out var tipo))
            return new BadRequestObjectResult(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);

        var comando = new AgregarSubFranja(turnoId, body.Franja, tipo, body.Inicio, body.Fin);

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
