using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;

// Issue #376 (MEF-ADR-0043 paso 3): endpoint HTTP DELETE para retirar la etiqueta de una
// categoria -- remocion veraz (la categoria deja de ser legible en el estado vigente) y sin
// payload (RFC 9110 §9.3.5: "a client SHOULD NOT generate content in a DELETE request"). MEF-ADR-
// 0006: [Function("RetirarEtiqueta")]; carpeta CON sufijo "Function" -- mismo criterio que los
// demas comandos del ciclo de vida.
// Route = "colaboradores/{id}/etiquetas/{categoria}" (kebab-case minusculo, MEF-ADR-0043 seccion 3,
// MISMA ruta que AsignarEtiqueta -- se distinguen por verbo HTTP): {id} es Identificacion.ToString()
// ("CC-79543210", issue #381) -- se parsea UNA vez con Identificacion.Parsear (unico punto de
// conversion string->Identificacion, MEF-ADR-0037 seccion 2) y un ArgumentException se traduce a
// 400 explicito, mismo mecanismo que ObtenerFichaColaborador.FunctionEndpoint. {categoria} viaja
// crudo -- el handler la normaliza via Etiqueta.NormalizarCategoria (Tell-don't-Ask). SIN body: no
// hay IRequestValidator involucrado (RetirarEtiquetaValidator, que validaba el body viejo, se
// elimino junto con este cambio -- sin body no hay nada que deserializar ni validar en ese punto).
// CA-ADR-0030 / MEF-ADR-0004 (precedente AnularTerminacionFunction.FunctionEndpoint): validar id de
// ruta (400) -> despachar comando -> InvalidOperationException -> 409 Conflict, KeyNotFoundException
// -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarEtiqueta")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "colaboradores/{id}/etiquetas/{categoria}")]
        HttpRequest req,
        string id,
        string categoria,
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

        var comando = new RetirarEtiqueta(identificacion.Tipo.ToString(), identificacion.Numero, categoria);

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
