using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// PUT porque reemplaza completo un valor atomico direccionable (MEF-ADR-0043 paso 2): asignar y
// reasignar son el mismo reemplazo. El codigo de sede viaja en el body, no como segmento de ruta --
// es un dato de tercero sin invariante URL-safe propia (MEF-ADR-0043 seccion 1.2).
// {id} se parsea UNA vez via IdentificacionDeRuta.TryParsear, con 400 explicito si falla
// (MEF-ADR-0037 seccion 2).
// Issue #659 (MEF-ADR-0004 "Respuestas HTTP" enmendado por harness#849): exito -> 204 No Content
// siempre -- el slot de sede existe por construccion, vacio o lleno; PUT lo reemplaza, nunca lo crea.
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

        return new NoContentResult();
    }
}
