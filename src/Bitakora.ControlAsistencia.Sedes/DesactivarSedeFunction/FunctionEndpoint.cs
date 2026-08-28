using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.DesactivarSedeFunction;

// Issue #459 (MEF-ADR-0043 paso 4): endpoint HTTP POST para desactivar una sede -- accion de
// negocio con verbo propio, sin body. MEF-ADR-0006: [Function("DesactivarSede")]; carpeta CON
// sufijo "Function". Route = "sedes/{codigo}:desactivar" (kebab-case minusculo).
// CA-ADR-0030 / MEF-ADR-0004 (precedente RetirarCentroDeCostosFunction.FunctionEndpoint): validar
// {codigo} de ruta (400) -> despachar comando -> InvalidOperationException -> 409 (CA-4, sede ya
// inactiva); KeyNotFoundException -> 404; exito -> 202 Accepted. Fase roja: stub minimo, el
// implementer completa la orquestacion real.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("DesactivarSede")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sedes/{codigo}:desactivar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
