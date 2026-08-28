using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Issue #457: endpoint HTTP PUT para reemplazar la ubicacion de una sede existente.
// MEF-ADR-0006: [Function("ActualizarUbicacionSede")]; carpeta CON sufijo "Function" -- el record
// del comando es homonimo del feature folder.
// Route = "sedes/{codigo}/ubicacion" (kebab-case minusculo, MEF-ADR-0043 paso 2): {codigo} no
// requiere parseo tipado adicional en el borde, misma razon que ModificarNombreSedeFunction.
// CA-ADR-0030 / MEF-ADR-0004 (precedente CorregirNombresFunction.FunctionEndpoint): validar body
// (400 via IRequestValidator) -> despachar comando -> KeyNotFoundException -> 404 (CA-4); exito ->
// 202 Accepted. Fase roja: stub minimo, el implementer completa la orquestacion real.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ActualizarUbicacionSede")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sedes/{codigo}/ubicacion")]
        HttpRequest req,
        string codigo,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
