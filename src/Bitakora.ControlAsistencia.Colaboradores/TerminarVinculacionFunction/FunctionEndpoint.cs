using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// POST {recurso}:{verbo} (MEF-ADR-0043 paso 4), no DELETE: la vinculacion sigue legible y la fecha
// efectiva exige body, que RFC 9110 9.3.5 desaconseja en DELETE. Verificado (harness#621): el
// worker aislado distingue {codigo}:terminar de {codigo}:anular-terminacion sobre el mismo segmento.
// {codigo} viaja intacto: la comparacion contra la vinculacion vigente vive en el aggregate.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("TerminarVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "colaboradores/{id}/vinculaciones/{codigo}:terminar")]
        HttpRequest req,
        string id,
        string codigo,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<TerminarVinculacionBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new TerminarVinculacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            codigo,
            body!.FechaEfectiva);

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
