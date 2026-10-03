using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Persiste SolicitudProgramacion antes de responder -> 201 Created sin Location: la solicitud no
// tiene GET canonico; el turno diario en ControlHoras es un efecto posterior al commit, no el
// recurso pedido (CA-ADR-0035).
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function(nameof(SolicitarProgramacionTurno))]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/solicitudes")]
        HttpRequest req,
        CancellationToken ct)
    {
        var (comando, error) = await requestValidator.ValidarAsync<SolicitarProgramacionTurno>(req, ct);
        if (error is not null)
            return error;

        ResultadoSolicitudProgramacion resultado;
        try
        {
            resultado = await commandRouter.InvokeAsync<SolicitarProgramacionTurno, ResultadoSolicitudProgramacion>(comando!, ct);
        }
        catch (PrecondicionComandoException ex)
        {
            switch (ex)
            {
                case RecursoYaExisteException:
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                case RecursoNoEncontradoException:
                    return new NotFoundObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new ObjectResult(resultado) { StatusCode = StatusCodes.Status201Created };
    }
}
