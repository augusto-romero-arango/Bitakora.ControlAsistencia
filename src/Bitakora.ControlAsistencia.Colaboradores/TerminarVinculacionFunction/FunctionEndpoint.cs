using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4, gate empirico de la seccion 8 verificado POSITIVO -- ver
// comentario en harness#621: Azure Functions worker aislado, Core Tools 4.6.0, distingue
// correctamente {codigo}:terminar de {codigo}:anular-terminacion sobre el mismo segmento base):
// endpoint HTTP POST para terminar la vinculacion vigente de un colaborador, ahora direccionada
// por su codigo. Terminar no es un create (paso 1), ni el reemplazo completo de un VO atomico
// direccionable (paso 2), ni una remocion veraz sin payload (paso 3: la vinculacion sigue legible,
// hay reingreso posible, y la fecha efectiva exige body que RFC 9110 9.3.5 prohibe en DELETE) --
// paso 4: accion de negocio con verbo propio, POST {recurso}:{verbo}.
// MEF-ADR-0006: [Function("TerminarVinculacion")]; carpeta CON sufijo "Function" -- mismo criterio
// que los demas comandos del ciclo de vida: el record del comando es homonimo del feature folder.
// Route = "colaboradores/{id}/vinculaciones/{codigo}:terminar" (kebab-case minusculo, MEF-ADR-0043
// seccion 3): {id} es Identificacion.ToString() ("CC-79543210") -- se parsea UNA vez con
// Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037 seccion
// 2), mismo mecanismo que CorregirNombresFunction.FunctionEndpoint (issue #377); {codigo} es el
// codigo de la vinculacion (URL-safe garantizado por #387) -- viaja intacto al comando interno, la
// comparacion contra el codigo vigente vive en el aggregate (Tell-don't-Ask, MEF-ADR-0012, CA-5).
// El body se reduce a FechaEfectiva (TerminarVinculacionBody); el endpoint compone el comando
// interno TerminarVinculacion (que conserva sus 4 campos primitivos, MEF-ADR-0039 decision 6) a
// partir de {id} + {codigo} + el body.
// Reemplaza el POST Colaboradores/Terminaciones (issue #349): la ruta vieja deja de existir (CA-7).
// CA-ADR-0030 / MEF-ADR-0004 (precedente CorregirNombresFunction.FunctionEndpoint): validar id de
// ruta (400) -> validar body (400 via IRequestValidator) -> despachar comando ->
// InvalidOperationException -> 409 Conflict (incluye CodigoNoCorresponde, CA-5, evaluada primero
// por el aggregate), KeyNotFoundException -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("TerminarVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "colaboradores/{id}/vinculaciones/{codigo}:terminar")]
        HttpRequest req,
        string id,
        string codigo,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<TerminarVinculacionBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new TerminarVinculacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            codigo,
            body!.FechaEfectiva);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
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
