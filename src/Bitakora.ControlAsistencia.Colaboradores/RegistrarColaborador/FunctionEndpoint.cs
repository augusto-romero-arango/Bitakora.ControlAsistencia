using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using ComandoRegistrarColaborador = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.RegistrarColaborador;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador;

// Issue #330: endpoint HTTP POST para registrar un colaborador bajo control de asistencia.
// MEF-ADR-0006: [Function("RegistrarColaborador")] como convencion de nombrado; carpeta sin sufijo
// "Function" (decision del planner, alineada con ObtenerTurnoVigente/ListarTurnosVigentes).
// Route = "Colaboradores": dominio y recurso son homonimos, un segundo segmento seria redundante.
// Flujo esperado (precedente CrearTurnoFunction.FunctionEndpoint):
//   validar request -> despachar comando -> InvalidOperationException -> 409 Conflict
//                                          -> exito -> 202 Accepted.
// STUB (fase roja, issue #330): el cuerpo completo queda para el implementer.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<ComandoRegistrarColaborador>(req, ct);
        if (error is not null)
            return error;

        try
        {
            await commandRouter.InvokeAsync(comando!, ct);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }

        return new AcceptedResult();
    }
}
