using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RetirarCentroDeCostosFunction;

// DELETE que remueve el centro de costos -- remocion veraz y SIN body (MEF-ADR-0043 paso 3), exito
// 204. Sin CC vigente es estado ya alcanzado (MEF-ADR-0004): el handler no lanza, tambien responde
// 204 sin evento. Sin catch de InvalidOperationException: este comando no tiene ninguna razon de
// rechazo que se traduzca a 409, y capturarla convertiria en 409 cualquier fallo inesperado del
// pipeline (MEF-ADR-0004, incidente #802) -- mismo criterio que RetirarDispositivo. No hay
// IRequestValidator, el {codigo} de ruta es lo unico que validar (MEF-ADR-0037 seccion 2).
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
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new NoContentResult();
    }
}
