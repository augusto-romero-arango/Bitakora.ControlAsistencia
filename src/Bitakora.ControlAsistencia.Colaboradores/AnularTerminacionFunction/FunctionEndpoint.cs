using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4, gate empirico de la seccion 8 verificado POSITIVO -- ver
// comentario en harness#621): endpoint HTTP POST para anular la terminacion registrada de la
// ultima vinculacion de un colaborador, ahora direccionada por su codigo. Anular no es un create
// (paso 1), ni el reemplazo de un VO atomico direccionable propio -- "la terminacion" no es un
// recurso direccionable independiente (paso 2), ni tiene sentido como remocion (paso 3: no hay
// sub-recurso que desaparezca) -- paso 4: accion de negocio con verbo propio, POST
// {recurso}:{verbo}.
// MEF-ADR-0006: [Function("AnularTerminacion")]; carpeta CON sufijo "Function" -- mismo criterio
// que los demas comandos del ciclo de vida: el record del comando es homonimo del feature folder.
// Route = "colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion" (kebab-case minusculo,
// MEF-ADR-0043 seccion 3): {id} es Identificacion.ToString() ("CC-79543210") -- se parsea UNA vez
// con Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037
// seccion 2); {codigo} es el codigo de la vinculacion (URL-safe garantizado por #387) -- viaja
// intacto al comando interno, la comparacion contra el codigo vigente vive en el aggregate
// (Tell-don't-Ask, MEF-ADR-0012, CA-5).
// SIN body: los tres campos del comando interno (TipoIdentificacion, NumeroIdentificacion, Codigo)
// viajan completos en la ruta -- este endpoint NO depende de IRequestValidator (nada que validar
// en el body, precedente muerto: AnularTerminacionValidator se elimino junto con el body).
// Reemplaza el POST Colaboradores/Terminaciones/Anulaciones (issue #354): la ruta vieja deja de
// existir (CA-7).
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar id
// de ruta (400) -> despachar comando -> InvalidOperationException -> 409 Conflict (incluye
// CodigoNoCorresponde, CA-5, evaluada primero por el aggregate), KeyNotFoundException -> 404
// NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("AnularTerminacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion")]
        HttpRequest req,
        string id,
        string codigo,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var comando = new AnularTerminacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            codigo);

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
