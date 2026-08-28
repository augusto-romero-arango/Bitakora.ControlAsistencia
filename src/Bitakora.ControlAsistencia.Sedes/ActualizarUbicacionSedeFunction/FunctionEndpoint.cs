using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// PUT que reemplaza completa la ubicacion (Ciudad+Direccion) como valor atomico direccionable por
// {codigo} (MEF-ADR-0043 paso 2). El {codigo} de ruta se valida aqui porque IRequestValidator solo
// cubre el body (MEF-ADR-0037 seccion 2: un unico chequeo del componente, con 400 explicito).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ActualizarUbicacionSede")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sedes/{codigo}/ubicacion")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var (body, error) = await requestValidator.ValidarAsync<ActualizarUbicacionSedeBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new ActualizarUbicacionSede(codigo, body!.Ciudad, body.Direccion);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
