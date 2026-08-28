using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Issue #457: endpoint HTTP PUT para reemplazar el nombre de una sede existente.
// MEF-ADR-0006: [Function("ModificarNombreSede")]; carpeta CON sufijo "Function" -- el record del
// comando es homonimo del feature folder.
// Route = "sedes/{codigo}/nombre" (kebab-case minusculo, MEF-ADR-0043 paso 2): {codigo} no requiere
// un parseo tipado adicional en el borde -- SedeAggregateRoot.ComputarStreamId lo concatena tal
// cual, y su URL-safety ya es invariante ganada en el issue previo #456 (MEF-ADR-0043 seccion 1.3).
// CA-ADR-0030 / MEF-ADR-0004 (precedente CorregirNombresFunction.FunctionEndpoint): validar body
// (400 via IRequestValidator) -> despachar comando -> KeyNotFoundException -> 404 (CA-4); exito ->
// 202 Accepted. Fase roja: stub minimo, el implementer completa la orquestacion real.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ModificarNombreSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sedes/{codigo}/nombre")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        var (body, error) = await requestValidator.ValidarAsync<ModificarNombreSedeBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new ModificarNombreSede(codigo, body!.Nombre);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
