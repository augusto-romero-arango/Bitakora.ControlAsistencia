using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;

// Dispositivo no instalado tambien responde 204 sin evento (MEF-ADR-0004); el 404 es solo para sede
// inexistente. {dispositivoId} no se re-valida: su charset URL-safe se exige en InstalarDispositivo.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarDispositivo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sedes/{codigo}/dispositivos/{dispositivoId}")]
        HttpRequest req,
        string codigo,
        string dispositivoId,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var comando = new RetirarDispositivo(codigo, dispositivoId);

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
