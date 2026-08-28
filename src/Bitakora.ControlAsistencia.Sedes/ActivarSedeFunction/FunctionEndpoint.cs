using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ActivarSedeFunction;

// Issue #459 (MEF-ADR-0043 paso 4): endpoint HTTP POST para reactivar una sede -- accion de negocio
// con verbo propio, sin body. MEF-ADR-0006: [Function("ActivarSede")]; carpeta CON sufijo
// "Function". Route = "sedes/{codigo}:activar" (kebab-case minusculo).
// CA-ADR-0030 / MEF-ADR-0004 (precedente RetirarCentroDeCostosFunction.FunctionEndpoint): validar
// {codigo} de ruta (400) -> despachar comando -> InvalidOperationException -> 409 (CA-3, sede ya
// activa); KeyNotFoundException -> 404; exito -> 202 Accepted. Fase roja: stub minimo, el
// implementer completa la orquestacion real.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("ActivarSede")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sedes/{codigo}:activar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
