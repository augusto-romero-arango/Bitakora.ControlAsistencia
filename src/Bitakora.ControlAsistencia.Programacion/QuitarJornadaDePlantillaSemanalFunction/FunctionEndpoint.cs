using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaSemanalFunction;

public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("QuitarJornadaDePlantillaSemanal")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "programacion/plantillas-semanales/{id}/jornada")]
        HttpRequest req,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
