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
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "programacion/plantillas-semanales/{id}/dias/{semana}/{dia}")]
        HttpRequest req,
        string id,
        string semana,
        string dia,
        CancellationToken ct)
        => throw new NotImplementedException();
}
