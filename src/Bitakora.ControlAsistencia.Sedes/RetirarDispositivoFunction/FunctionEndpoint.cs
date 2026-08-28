using Bitakora.ControlAsistencia.Sedes.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Sedes.RetirarDispositivoFunction;

// DELETE que remueve verazmente un sub-recurso direccionable sin payload -- MEF-ADR-0043 paso 3.
// Sin IRequestValidator: no hay body que deserializar ni validar en este punto. El {codigo} de
// ruta es lo unico que se valida aqui (MEF-ADR-0037 seccion 2); {dispositivoId} no lleva invariante
// propia en este endpoint -- la URL-safe del DispositivoId se gano en el borde del POST
// (InstalarDispositivo), no se re-valida al retirar.
// Sin catch de InvalidOperationException: este comando no tiene ninguna razon de rechazo que se
// traduzca a 409 -- retirar lo no instalado es un sub-recurso ausente (404). Agregarlo "por
// simetria" con los demas endpoints convertiria en 409 cualquier fallo inesperado del pipeline.
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
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
