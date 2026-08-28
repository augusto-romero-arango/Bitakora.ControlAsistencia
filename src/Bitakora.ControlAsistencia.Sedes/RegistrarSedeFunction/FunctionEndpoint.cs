using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RegistrarSedeFunction;

// Issue #456: endpoint HTTP POST para registrar una sede.
// MEF-ADR-0006: [Function("RegistrarSede")] como convencion de nombrado; carpeta CON sufijo
// "Function" porque el record del comando es homonimo del feature folder.
// Route = "sedes" (kebab-case minusculo, MEF-ADR-0043 seccion 3): dominio y recurso son homonimos.
// MEF-ADR-0004 (precedente RegistrarColaboradorFunction.FunctionEndpoint): validar request (400 via
// IRequestValidator) -> despachar comando -> InvalidOperationException -> 409 Conflict; exito -> 202.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sedes")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<RegistrarSede>(req, ct);
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

        return new AcceptedResult();
    }
}
