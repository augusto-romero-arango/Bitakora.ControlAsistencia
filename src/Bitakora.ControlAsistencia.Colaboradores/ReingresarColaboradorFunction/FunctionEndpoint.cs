using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction;

// Issue #350: endpoint HTTP POST para reingresar a un colaborador bajo control de asistencia.
// MEF-ADR-0006: [Function("ReingresarColaborador")]; carpeta CON sufijo "Function" -- mismo
// criterio que RegistrarColaboradorFunction/TerminarVinculacionFunction: el record del comando es
// homonimo del feature folder.
// Route = "Colaboradores/Reingresos": sub-recurso gemelo de Colaboradores/Terminaciones (#349) --
// la identificacion viaja en el body porque su representacion ("CC:79543210") contiene ":",
// hostil como segmento de URL.
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar
// request (400 via IRequestValidator) -> despachar comando -> InvalidOperationException -> 409
// Conflict, KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ReingresarColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/Reingresos")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<ReingresarColaborador>(req, ct);
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
