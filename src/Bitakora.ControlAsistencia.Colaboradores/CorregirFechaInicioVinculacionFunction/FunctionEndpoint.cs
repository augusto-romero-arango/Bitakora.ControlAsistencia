using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// POST {recurso}:{verbo} (MEF-ADR-0043 paso 4): FechaInicio es una propiedad escalar del
// aggregate, no un VO atomico direccionable. {codigo} viaja intacto: la comparacion contra la
// vinculacion vigente vive en el aggregate.
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
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

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
