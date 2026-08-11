using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaboradorFunction;

// Issue #330: endpoint HTTP POST para registrar un colaborador bajo control de asistencia.
// MEF-ADR-0006: [Function("RegistrarColaborador")] como convencion de nombrado; carpeta CON sufijo
// "Function" porque es un comando HTTP y el record del comando es homonimo del feature folder --
// las carpetas sin sufijo (ObtenerTurnoVigente/ListarTurnosVigentes) son queries GET, que no tienen
// record de comando con el que colisionar. Sin el sufijo, este archivo no podria nombrar su propio
// comando sin un alias de using.
// Route = "Colaboradores": dominio y recurso son homonimos, un segundo segmento seria redundante.
// MEF-ADR-0004 (precedente CrearTurnoFunction.FunctionEndpoint): validar request (400 via
// IRequestValidator) -> despachar comando -> InvalidOperationException -> 409 Conflict; exito -> 202.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("RegistrarColaborador")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "Colaboradores")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<RegistrarColaborador>(req, ct);
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
