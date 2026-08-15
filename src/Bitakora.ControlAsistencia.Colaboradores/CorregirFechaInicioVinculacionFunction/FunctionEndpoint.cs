using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4, gate empirico de la seccion 8 verificado POSITIVO -- ver
// comentario en harness#621): endpoint HTTP POST para corregir la fecha de inicio de la ultima
// vinculacion de un colaborador, ahora direccionada por su codigo. FechaInicio es una propiedad
// escalar del aggregate, no un VO atomico direccionable -- ni PUT (paso 2) ni DELETE (paso 3)
// aplican -- paso 4: accion de negocio con verbo propio, POST {recurso}:{verbo}.
// MEF-ADR-0006: [Function("CorregirFechaInicioVinculacion")]; carpeta CON sufijo "Function" --
// mismo criterio que los demas comandos del ciclo de vida: el record del comando es homonimo del
// feature folder.
// Route = "colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio" (kebab-case minusculo,
// MEF-ADR-0043 seccion 3): {id} es Identificacion.ToString() ("CC-79543210") -- se parsea UNA vez
// con Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037
// seccion 2); {codigo} es el codigo de la vinculacion (URL-safe garantizado por #387) -- viaja
// intacto al comando interno, la comparacion contra el codigo vigente vive en el aggregate
// (Tell-don't-Ask, MEF-ADR-0012, CA-5).
// El body se reduce a FechaCorregida (CorregirFechaInicioVinculacionBody); el endpoint compone el
// comando interno CorregirFechaInicioVinculacion (que conserva sus 4 campos primitivos,
// MEF-ADR-0039 decision 6) a partir de {id} + {codigo} + el body.
// Reemplaza el POST Colaboradores/FechasInicio (issue #352): la ruta vieja deja de existir (CA-7).
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint post-#379):
// validar id de ruta (400) -> validar body (400 via IRequestValidator) -> despachar comando ->
// InvalidOperationException -> 409 Conflict (incluye CodigoNoCorresponde, CA-5, evaluada primero
// por el aggregate, ANTES incluso de la idempotencia SinCambios), KeyNotFoundException -> 404
// NotFound; exito -> 202 Accepted (incluye el silencio de SinCambios).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CorregirFechaInicioVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio")]
        HttpRequest req,
        string id,
        string codigo,
        CancellationToken ct)
    {
        Identificacion identificacion;
        try
        {
            identificacion = Identificacion.Parsear(id);
        }
        catch (ArgumentException)
        {
            return new BadRequestObjectResult(
                "El id de la ruta es invalido -- debe tener la forma {Tipo}-{Numero}");
        }

        var (body, error) = await requestValidator.ValidarAsync<CorregirFechaInicioVinculacionBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new CorregirFechaInicioVinculacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            codigo,
            body!.FechaCorregida);

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
