using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Issue #465 (MEF-ADR-0043 paso 2): endpoint HTTP PUT para asignar (o reasignar por completo) la
// sede del colaborador -- reemplazo del VO atomico "sede", direccionable por {id} unicamente (el
// codigo viaja en el body, sin segmento de ruta adicional -- precedente AsignarEtiquetaFunction,
// simplificado: aqui no hay equivalente a {categoria}).
// MEF-ADR-0006: [Function("AsignarSede")]; carpeta CON sufijo "Function".
// Route = "colaboradores/{id}/sede" (kebab-case minusculo, MEF-ADR-0043 seccion 3): {id} es
// Identificacion.ToString() ("CC-79543210"), parseado UNA vez via IdentificacionDeRuta.TryParsear
// (400 explicito si falla, MEF-ADR-0037 seccion 2), compartido con los demas endpoints del dominio
// que reciben {id}.
// CA-ADR-0030 / MEF-ADR-0004 (precedente AsignarEtiquetaFunction.FunctionEndpoint; MEF-ADR-0043
// seccion 2 paso 2: el 409 de un PUT es una instancia mas de "declinar con resultado", RFC 9110
// §9.3.4): validar id de ruta (400) -> validar body (400 via IRequestValidator) -> despachar
// comando -> InvalidOperationException -> 409 Conflict, KeyNotFoundException -> 404 NotFound;
// exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "colaboradores/{id}/sede")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<AsignarSedeBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarSede(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            body!.CodigoSede);

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
