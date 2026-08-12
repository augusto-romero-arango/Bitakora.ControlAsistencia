using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// Issue #354: endpoint HTTP POST para anular la terminacion registrada de la ultima vinculacion de
// un colaborador. MEF-ADR-0006: [Function("AnularTerminacion")]; carpeta CON sufijo "Function" --
// mismo criterio que los demas comandos del ciclo de vida: el record del comando es homonimo del
// feature folder.
// Route = "Colaboradores/Terminaciones/Anulaciones": la anulacion como sub-recurso de las
// terminaciones (#349) -- identificacion en el body porque su representacion ("CC:79543210")
// contiene ":", hostil como segmento de URL.
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar
// request (400 via IRequestValidator) -> despachar comando -> InvalidOperationException -> 409
// Conflict, KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AnularTerminacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/Terminaciones/Anulaciones")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<AnularTerminacion>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(comando!, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
