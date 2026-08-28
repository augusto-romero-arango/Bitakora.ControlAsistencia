using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.InstalarDispositivoFunction;

// POST que agrega una entidad (interna) a la coleccion del aggregate -- MEF-ADR-0043 paso 1. El
// {codigo} de ruta se valida aqui porque IRequestValidator solo cubre el body (MEF-ADR-0037
// seccion 2: un unico chequeo del componente, con 400 explicito).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("InstalarDispositivo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sedes/{codigo}/dispositivos")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var (body, error) = await requestValidator.ValidarAsync<InstalarDispositivoBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new InstalarDispositivo(codigo, body!.DispositivoId);

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

        return new AcceptedResult();
    }
}
