using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;

// Accion de negocio con verbo propio (MEF-ADR-0043 paso 4): la clave natural HH:mm de la hija
// contiene ":", fuera del charset URL-safe -- POST "{recurso}:{verbo}". El {id} de ruta se valida
// a mano (MEF-ADR-0037 seccion 2); el body lo valida IRequestValidator.
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

        // Enum.Parse lanzaria fuera del try (500) si el body llegara con un tipo no parseable: el
        // 400 canonico de este caso lo produce QuitarSubFranjaBodyValidator, y esta guarda reemite
        // su mismo mensaje .resx (compartido con AgregarSubFranjaBodyValidator, #603) para que el
        // borde nunca dependa de que el validator este registrado.
        if (!Enum.TryParse<TipoSubFranja>(body!.Tipo, ignoreCase: true, out var tipo))
            return new BadRequestObjectResult(AgregarSubFranjaBodyValidator.Mensajes.TipoDesconocido);

        var comando = new QuitarSubFranja(turnoId, body.Franja, tipo, body.Inicio);

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
