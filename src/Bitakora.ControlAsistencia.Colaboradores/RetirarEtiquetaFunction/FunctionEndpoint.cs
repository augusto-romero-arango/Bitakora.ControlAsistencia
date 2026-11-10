using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction;

// DELETE (MEF-ADR-0043 paso 3): remocion veraz -- la categoria deja de ser legible en el estado
// vigente -- y sin payload. Misma ruta que AsignarEtiqueta; se distinguen por verbo HTTP.
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
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        // Ningun validator cubre {categoria}: un segmento en blanco ("%20") hace match con la ruta y,
        // sin esta guarda, llega a Etiqueta.NormalizarCategoria, cuyo ArgumentException nadie
        // traduce (500, no 400).
        if (string.IsNullOrWhiteSpace(categoria))
            return new BadRequestObjectResult("La categoria de la ruta no puede estar en blanco");

        var comando = new RetirarEtiqueta(identificacion.Tipo.ToString(), identificacion.Numero, categoria);

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
