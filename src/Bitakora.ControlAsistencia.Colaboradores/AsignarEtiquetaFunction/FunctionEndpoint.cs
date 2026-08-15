using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #376 (MEF-ADR-0043 paso 2): endpoint HTTP PUT para asignar (o sobrescribir por completo)
// la etiqueta de una categoria -- reemplazo del VO atomico Etiqueta, direccionable por categoria.
// MEF-ADR-0006: [Function("AsignarEtiqueta")]; carpeta CON sufijo "Function" -- mismo criterio que
// los demas comandos del ciclo de vida.
// Route = "colaboradores/{id}/etiquetas/{categoria}" (kebab-case minusculo, MEF-ADR-0043 seccion 3):
// {id} es Identificacion.ToString() ("CC-79543210", issue #381) -- se parsea UNA vez con
// Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037 seccion 2)
// y un ArgumentException se traduce a 400 explicito, mismo mecanismo que
// ObtenerFichaColaborador.FunctionEndpoint. {categoria} viaja crudo (sin normalizar en el endpoint
// -- Etiqueta.Crear normaliza internamente, Tell-don't-Ask, mismo criterio que el resto del ciclo
// de vida). El body se redujo a { "valor": "..." } (AsignarEtiquetaBody); el endpoint compone el
// comando interno AsignarEtiqueta (que conserva sus 4 campos primitivos, MEF-ADR-0039 decision 6)
// a partir de {id} + {categoria} + Valor.
// CA-ADR-0030 / MEF-ADR-0004 (precedente AnularTerminacionFunction.FunctionEndpoint; MEF-ADR-0043
// seccion 2 paso 2: el 409 de un PUT es una instancia mas de "declinar con resultado", RFC 9110
// §9.3.4): validar id de ruta (400) -> validar body (400 via IRequestValidator) -> despachar
// comando -> InvalidOperationException -> 409 Conflict, KeyNotFoundException -> 404 NotFound;
// exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarEtiqueta")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "colaboradores/{id}/etiquetas/{categoria}")]
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

        var (body, error) = await requestValidator.ValidarAsync<AsignarEtiquetaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarEtiqueta(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            categoria,
            body!.Valor);

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
