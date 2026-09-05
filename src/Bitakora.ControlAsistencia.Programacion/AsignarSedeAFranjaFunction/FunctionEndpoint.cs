using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarSedeAFranjaFunction;

// Accion de negocio con verbo propio (MEF-ADR-0043 paso 4): la sede es un VO atomico pero no
// direccionable por URL -- la franja contenedora tiene clave natural HH:mm, con ":" fuera del
// charset URL-safe (seccion 1.1) -- y la misma accion cubre asignar y retirar (payload con sede o
// sin ella) -- POST "{recurso}:{verbo}". El {id} de ruta se valida a mano (MEF-ADR-0037 seccion 2);
// el body lo valida IRequestValidator.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarSedeAFranja")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:asignar-sede-franja")]
        HttpRequest req,
        string id,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
