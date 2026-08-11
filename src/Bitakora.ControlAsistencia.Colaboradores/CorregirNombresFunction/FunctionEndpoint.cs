using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #351: endpoint HTTP POST para corregir los nombres de un colaborador existente.
// MEF-ADR-0006: [Function("CorregirNombres")]; carpeta CON sufijo "Function" -- mismo criterio que
// RegistrarColaboradorFunction/TerminarVinculacionFunction/ReingresarColaboradorFunction: el
// record del comando es homonimo del feature folder.
// Route = "Colaboradores/Nombres": el recurso que se reemplaza -- identificacion en el body porque
// su representacion ("CC:79543210") contiene ":", hostil como segmento de URL.
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar
// request (400 via IRequestValidator) -> despachar comando -> KeyNotFoundException -> 404 NotFound
// (sin 409: este comando no tiene reglas de estado); exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CorregirNombres")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores/Nombres")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<CorregirNombres>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(comando!, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
