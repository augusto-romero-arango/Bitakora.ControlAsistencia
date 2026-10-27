using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarJornadaDePlantillaTurnosFunction;

public class FunctionEndpoint
{
    private readonly ICommandRouter _commandRouter;

    public FunctionEndpoint(ICommandRouter commandRouter) => _commandRouter = commandRouter;

    [Function("QuitarJornadaDePlantillaTurnos")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete",
            Route = "programacion/plantillas-turnos/{id}/jornada")]
        HttpRequest req,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
