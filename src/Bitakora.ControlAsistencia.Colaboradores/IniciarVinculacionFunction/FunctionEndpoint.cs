using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Issue #378 (MEF-ADR-0043 paso 1): endpoint HTTP POST que inicia una vinculacion nueva sobre un
// colaborador existente -- create disfrazado: emite el MISMO evento que RegistrarColaborador
// (VinculacionIniciada). Absorbe y reemplaza a ReingresarColaboradorFunction (issue #350): mismo
// mecanismo "declinar con resultado" (CA-ADR-0030), misma invariante de no-solape.
// MEF-ADR-0006: [Function("IniciarVinculacion")]; carpeta CON sufijo "Function" -- mismo criterio
// que los demas comandos del ciclo de vida: el record del comando es homonimo del feature folder.
// Route = "colaboradores/{id}/vinculaciones" (kebab-case minusculo, MEF-ADR-0043 seccion 3): {id}
// es Identificacion.ToString() ("CC-79543210", issue #381) -- se parsea UNA vez con
// Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037 seccion 2)
// y un ArgumentException se traduce a 400 explicito, mismo mecanismo que
// CorregirNombresFunction.FunctionEndpoint (issue #377) / AsignarEtiquetaFunction.FunctionEndpoint
// (issue #376) / ObtenerFichaColaborador.FunctionEndpoint.
// El body se reduce a CodigoColaborador + FechaInicio (IniciarVinculacionBody); el endpoint compone
// el comando interno IniciarVinculacion (que conserva sus 4 campos primitivos, MEF-ADR-0039
// decision 6) a partir de {id} + esos 2 campos del body.
// Reemplaza el POST Colaboradores/Reingresos (issue #350): la ruta vieja deja de existir (CA-6).
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar id
// de ruta (400) -> validar body (400 via IRequestValidator) -> despachar comando ->
// InvalidOperationException -> 409 Conflict (invariante de no-solape violada), KeyNotFoundException
// -> 404 NotFound; exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("IniciarVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "colaboradores/{id}/vinculaciones")]
        HttpRequest req,
        string id,
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

        var (body, error) = await requestValidator.ValidarAsync<IniciarVinculacionBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new IniciarVinculacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            body!.CodigoColaborador,
            body.FechaInicio);

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
