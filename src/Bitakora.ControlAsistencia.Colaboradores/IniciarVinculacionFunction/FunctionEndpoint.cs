using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Create disfrazado (MEF-ADR-0043 paso 1): emite el MISMO evento que RegistrarColaborador
// (VinculacionIniciada).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("IniciarVinculacion")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "colaboradores/{id}/vinculaciones")]
        HttpRequest req,
        string id,
        CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<IniciarVinculacionBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new IniciarVinculacion(
            identificacion.Tipo.ToString(),
            identificacion.Numero,
            body!.CodigoColaborador,
            body.FechaInicio,
            body.CodigoSede);

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

        return new CreatedResult($"/api/colaboradores/fichas/{identificacion}", null);
    }
}
