using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarTurnoADiaDePlantillaTurnos")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "programacion/plantillas-turnos/{id}/dias/{dia}")]
        HttpRequest req,
        string id,
        string dia,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var plantillaId))
            return new BadRequestObjectResult("El id de la plantilla no es un Guid valido");

        if (!int.TryParse(dia, out var diaNumero) || diaNumero < 1)
            return new BadRequestObjectResult("El dia debe ser un entero mayor o igual a 1");

        var (body, error) = await requestValidator.ValidarAsync<AsignarTurnoADiaDePlantillaTurnosBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarTurnoADiaDePlantillaTurnos(plantillaId, diaNumero, body!.TurnoId);

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
                case ReglaDeNegocioDeclinadaException:
                    return new ConflictObjectResult(ex.Message);
                default:
                    throw;
            }
        }

        return new NoContentResult();
    }
}
