using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #377 (MEF-ADR-0043 paso 2): endpoint HTTP PUT para corregir (reemplazar por completo) los
// nombres de un colaborador existente -- reemplazo del VO atomico NombreColaborador, direccionable
// por {id}. MEF-ADR-0006: [Function("CorregirNombres")]; carpeta CON sufijo "Function" -- mismo
// criterio que los demas comandos del ciclo de vida: el record del comando es homonimo del feature
// folder.
// Route = "colaboradores/{id}/nombres" (kebab-case minusculo, MEF-ADR-0043 seccion 3): {id} es
// Identificacion.ToString() ("CC-79543210", issue #381) -- se parsea UNA vez con
// Identificacion.Parsear (unico punto de conversion string->Identificacion, MEF-ADR-0037 seccion 2)
// y un ArgumentException se traduce a 400 explicito, mismo mecanismo que
// AsignarEtiquetaFunction.FunctionEndpoint (issue #376) / ObtenerFichaColaborador.FunctionEndpoint.
// El body se reduce a los 4 campos del nombre (CorregirNombresBody); el endpoint compone el comando
// interno CorregirNombres (que conserva sus 6 campos primitivos, MEF-ADR-0039 decision 6) a partir
// de {id} + los 4 campos del body.
// Reemplaza el POST Colaboradores/Nombres (issue #351): la ruta vieja deja de existir (CA-5).
// CA-ADR-0030 / MEF-ADR-0004 (precedente TerminarVinculacionFunction.FunctionEndpoint): validar id
// de ruta (400) -> validar body (400 via IRequestValidator) -> despachar comando ->
// KeyNotFoundException -> 404 NotFound (sin 409: este comando no tiene reglas de estado, CA-2);
// exito -> 202 Accepted.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CorregirNombres")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "colaboradores/{id}/nombres")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<CorregirNombresBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new CorregirNombres(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            body!.PrimerNombre,
            body.SegundoNombre,
            body.PrimerApellido,
            body.SegundoApellido);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
