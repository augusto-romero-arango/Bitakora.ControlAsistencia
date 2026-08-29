using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.RetirarTurnoFunction;

// DELETE que retira el turno del catalogo -- remocion veraz y SIN body (MEF-ADR-0043 paso 3), asi
// que el {id} de ruta es lo unico que validar (MEF-ADR-0037 seccion 2).
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarTurno")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "programacion/turnos/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
