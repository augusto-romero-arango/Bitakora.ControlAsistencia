using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ModificarNombreSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sedes/{codigo}/nombre")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var (body, error) = await requestValidator.ValidarAsync<ModificarNombreSedeBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new ModificarNombreSede(codigo, body!.Nombre);

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
