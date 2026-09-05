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
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:quitar-subfranja")]
        HttpRequest req,
        string id,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
