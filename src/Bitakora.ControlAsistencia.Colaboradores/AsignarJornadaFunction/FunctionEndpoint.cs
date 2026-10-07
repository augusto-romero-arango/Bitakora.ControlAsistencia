using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarJornada")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "colaboradores/{id}/jornada")]
        HttpRequest req, string id, CancellationToken ct)
    {
        if (!IdentificacionDeRuta.TryParsear(id, out var identificacion, out var errorDeId))
            return errorDeId;

        var (body, error) = await requestValidator.ValidarAsync<AsignarJornadaBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarJornada(identificacion.Tipo.ToString(), identificacion.Numero, body!.JornadaId);
        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
