using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;

// Issue #458 (MEF-ADR-0043 paso 3): endpoint HTTP DELETE para retirar el centro de costos de una
// sede -- remocion veraz y sin payload (RFC 9110 SS9.3.5). MEF-ADR-0006:
// [Function("RetirarCentroDeCostos")]; carpeta CON sufijo "Function".
// Route = "sedes/{codigo}/centro-de-costos" (MISMA ruta que AsignarCentroDeCostos -- se distinguen
// por verbo HTTP).
// CA-ADR-0030 / MEF-ADR-0004 (precedente RetirarEtiquetaFunction.FunctionEndpoint): validar
// {codigo} de ruta (400) -> despachar comando -> InvalidOperationException -> 409 (CA-4, sin CC
// vigente); KeyNotFoundException -> 404; exito -> 202 Accepted. Fase roja: stub minimo, el
// implementer completa la orquestacion real.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarCentroDeCostos")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sedes/{codigo}/centro-de-costos")]
        HttpRequest req,
        string codigo,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
