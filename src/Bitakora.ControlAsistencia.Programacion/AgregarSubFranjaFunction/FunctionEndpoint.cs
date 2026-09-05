using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;

// Accion de negocio con verbo propio: la sub-franja es un VO sin identidad propia, direccionada
// por franja + hora de inicio (MEF-ADR-0043 paso 4) -- POST "{recurso}:{verbo}". El {id} de ruta
// se valida a mano (MEF-ADR-0037 seccion 2); el body lo valida IRequestValidator.
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

        // Enum.Parse lanzaria fuera del try (500) si el body llegara con un tipo no parseable:
        // el 400 canonico de este caso lo produce AgregarSubFranjaBodyValidator, y esta guarda
        // reemite su mismo mensaje .resx para que el borde nunca dependa de que el validator este
        // registrado (MEF-ADR-0037 seccion 2: parseo tipado con 400 explicito).
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
