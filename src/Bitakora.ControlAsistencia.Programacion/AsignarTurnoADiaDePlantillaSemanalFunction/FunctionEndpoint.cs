using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction;

// Issue #621: PUT reemplaza completo un VO atomico direccionable -- el turno del dia (MEF-ADR-0043
// paso 2). El slot (semana, dia) existe por construccion (7 x N slots, CA-ADR-0034), asi que este
// PUT nunca "crea": 204 No Content, tambien cuando el turno ya era el mismo (SinCambios). Nunca
// AcceptedResult. {semana}/{dia} viajan como string + int.TryParse (no route constraints): el
// {dia} fuera de 1..7 se traduce a 400 ANTES de despachar (DiaSemana.Desde lanza ArgumentException,
// MEF-ADR-0004 capa 1), sin invocar el router.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarTurnoADiaDePlantillaSemanal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "programacion/plantillas-semanales/{id}/dias/{semana}/{dia}")]
        HttpRequest req,
        string id,
        string semana,
        string dia,
        CancellationToken ct)
    {
        if (!Guid.TryParse(id, out var plantillaId))
            return new BadRequestObjectResult("El id de la plantilla no es un Guid valido");

        if (!int.TryParse(semana, out var semanaNumero) || semanaNumero < 1)
            return new BadRequestObjectResult("La semana debe ser un entero mayor o igual a 1");

        if (!int.TryParse(dia, out var diaNumero))
            return new BadRequestObjectResult("El dia no es un entero valido");

        DiaSemana diaSemana;
        try
        {
            diaSemana = DiaSemana.Desde(diaNumero);
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }

        var (body, error) = await requestValidator.ValidarAsync<AsignarTurnoADiaDePlantillaSemanalBody>(req, ct);
        if (error is not null)
            return error;

        var comando = new AsignarTurnoADiaDePlantillaSemanal(plantillaId, semanaNumero, diaSemana, body!.TurnoId);

        try
        {
            await commandRouter.InvokeAsync(comando, ct);
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(ex.Message);
        }

        return new NoContentResult();
    }
}
