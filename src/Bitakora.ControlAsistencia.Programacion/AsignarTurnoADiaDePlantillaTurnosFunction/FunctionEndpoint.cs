using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("AsignarTurnoADiaDePlantillaTurnos")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "programacion/plantillas-turnos/{id}/dias/{dia}")]
        HttpRequest req,
        string id,
        string dia,
        CancellationToken ct)
        => throw new NotImplementedException();
}
