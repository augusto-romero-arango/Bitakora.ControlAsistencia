using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Issue #352: endpoint HTTP POST para corregir la fecha de inicio de la ultima vinculacion de un
// colaborador. MEF-ADR-0006: [Function("CorregirFechaInicioVinculacion")]; carpeta CON sufijo
// "Function" -- mismo criterio que los demas comandos del ciclo de vida: el record del comando es
// homonimo del feature folder.
// Route = "Colaboradores/FechasInicio": el recurso que se reemplaza, gemelo de
// Colaboradores/Nombres (#351) -- identificacion en el body, decision vigente hasta #378 (rutas
// orientadas a recurso): el issue #381 cambio la representacion a "CC-79543210" justamente para que
// la llave sea apta como segmento de URI.
// CA-ADR-0030 / MEF-ADR-0004 (precedente ReingresarColaboradorFunction.FunctionEndpoint): validar
// request (400 via IRequestValidator) -> despachar comando -> InvalidOperationException -> 409
// Conflict, KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CorregirFechaInicioVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/FechasInicio")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<CorregirFechaInicioVinculacion>(req, ct);
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
