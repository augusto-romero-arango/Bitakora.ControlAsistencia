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
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "programacion/plantillas-semanales/{id}/dias/{semana}/{dia}")]
        HttpRequest req,
        string id,
        string semana,
        string dia,
        CancellationToken ct)
        => throw new NotImplementedException();
}
