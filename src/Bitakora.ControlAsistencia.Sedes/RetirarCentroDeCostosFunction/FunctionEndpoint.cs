using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;

// Sin CC vigente tambien responde 204 sin evento: es estado ya alcanzado (MEF-ADR-0004).
// Comparte segmento con AsignarCentroDeCostos (PUT): ambos deben declarar su verbo o uno
// capturaria al otro (MEF-ADR-0006).
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarCentroDeCostos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sedes/{codigo}/centro-de-costos")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
    {
        if (!CodigoSedeDeRuta.EsValido(codigo, out var errorDeCodigo))
            return errorDeCodigo;

        var comando = new RetirarCentroDeCostos(codigo);

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
