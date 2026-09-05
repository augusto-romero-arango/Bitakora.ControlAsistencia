using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.RetirarPlantillaSemanalFunction;

// DELETE retira la plantilla del catalogo -- remocion veraz y SIN body (MEF-ADR-0043 paso 3). Ya
// retirada responde 204 igual que recien retirada (SinCambios, CA-ADR-0030) -- DELETE es
// idempotente (RFC 9110 seccion 9.2.2). Nunca AcceptedResult.
public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("RetirarPlantillaSemanal")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "programacion/plantillas-semanales/{id}")]
        HttpRequest req,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
