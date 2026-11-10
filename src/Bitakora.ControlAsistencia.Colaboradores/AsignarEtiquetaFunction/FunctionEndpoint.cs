using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// PUT: reemplazo completo del VO atomico Etiqueta, direccionable por categoria (MEF-ADR-0043 paso 2).
// {categoria} viaja cruda: la normaliza Etiqueta.Crear.
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
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        // Ningun validator cubre {categoria}: un segmento en blanco ("%20") hace match con la ruta y,
        // sin esta guarda, llega a Etiqueta.Crear, cuyo ArgumentException nadie traduce (500, no 400).
        if (string.IsNullOrWhiteSpace(categoria))
            return new BadRequestObjectResult("La categoria de la ruta no puede estar en blanco");

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
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
