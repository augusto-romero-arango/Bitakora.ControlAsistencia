using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;

// Accion de negocio con verbo propio: la franja es un VO sin identidad propia, direccionado por
// su hora de inicio -- ni crea una entidad, ni reemplaza un VO direccionable, ni remueve
// (MEF-ADR-0043 paso 4) -- POST "{recurso}:{verbo}". El {id} de ruta se valida a mano
// (MEF-ADR-0037 seccion 2); el body lo valida IRequestValidator.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AgregarFranja")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos/{id}:agregar-franja")]
        HttpRequest req,
        string id,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
