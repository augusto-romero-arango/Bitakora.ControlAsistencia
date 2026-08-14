using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #349: endpoint HTTP POST para terminar la vinculacion vigente de un colaborador.
// MEF-ADR-0006: [Function("TerminarVinculacion")]; carpeta CON sufijo "Function" -- mismo criterio
// que RegistrarColaboradorFunction (issue #330, refactor 63270fa): el record del comando es
// homonimo del feature folder, y las carpetas sin sufijo son queries GET sin ese conflicto.
// Route = "Colaboradores/Terminaciones": la terminacion como sub-recurso (estilo
// programacion/solicitudes) -- la identificacion viaja en el body, decision vigente hasta #378
// (rutas orientadas a recurso): el issue #381 cambio la representacion a "CC-79543210" justamente
// para que la llave sea apta como segmento de URI.
// CA-ADR-0030 / MEF-ADR-0004 (precedente SolicitarProgramacionTurnoFunction.FunctionEndpoint):
// validar request (400 via IRequestValidator) -> despachar comando -> InvalidOperationException
// -> 409 Conflict, KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("TerminarVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/Terminaciones")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<TerminarVinculacion>(req, ct);
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
