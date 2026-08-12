using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #355: endpoint HTTP POST para asignar (o sobrescribir) una etiqueta dinamica a la
// vinculacion vigente de un colaborador. MEF-ADR-0006: [Function("AsignarEtiqueta")]; carpeta CON
// sufijo "Function" -- mismo criterio que los demas comandos del ciclo de vida: el record del
// comando es homonimo del feature folder.
// Route = "Colaboradores/Etiquetas": identificacion en el body porque su representacion
// ("CC:79543210") contiene ":", hostil como segmento de URL.
// CA-ADR-0030 / MEF-ADR-0004 (precedente AnularTerminacionFunction.FunctionEndpoint): validar
// request (400 via IRequestValidator) -> despachar comando -> InvalidOperationException -> 409
// Conflict, KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarEtiqueta")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/Etiquetas")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<AsignarEtiqueta>(req, ct);
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
