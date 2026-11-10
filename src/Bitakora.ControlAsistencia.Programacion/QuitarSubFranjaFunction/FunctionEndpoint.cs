using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// POST "{recurso}:{verbo}" (MEF-ADR-0043 paso 4): la clave natural HH:mm de la hija contiene ":",
// fuera del charset URL-safe.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("QuitarSubFranja")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:quitar-subfranja")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var turnoId))
            return new BadRequestObjectResult("El id del turno no es un Guid valido");

        var (body, error) = await requestValidator.ValidarAsync<QuitarSubFranjaBody>(req, ct);
        if (error is not null)
            return error;

        // Reemite el 400 del validator para que el borde no dependa de que el validator este registrado.
        if (!Enum.TryParse<TipoSubFranja>(body!.Tipo, ignoreCase: true, out var tipo))
            return new BadRequestObjectResult(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);

        var comando = new QuitarSubFranja(turnoId, body.Franja, tipo, body.Inicio);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
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
