using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarTurnoDeDiaDePlantillaSemanalFunction;

// Issue #622: DELETE vacia el slot (semana, dia) de la plantilla -- remocion veraz y SIN body
// (MEF-ADR-0043 paso 3). Comparte segmento de ruta con el PUT de #621: cada uno declara su propio
// verbo (MEF-ADR-0006). SinCambios (dia ya vacio) responde 204 igual que Quitado -- DELETE es
// idempotente (RFC 9110 seccion 9.2.2). Nunca AcceptedResult.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("QuitarTurnoDeDiaDePlantillaSemanal")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
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

        var comando = new QuitarTurnoDeDiaDePlantillaSemanal(plantillaId, semanaNumero, diaSemana);

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
