using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// PUT: reemplazo completo del VO atomico NombreColaborador (MEF-ADR-0043 paso 2).
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
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
