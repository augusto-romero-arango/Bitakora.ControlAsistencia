using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction;

public class FunctionEndpoint(ICommandRouter commandRouter)
{
    [Function("QuitarModalidadDeJornada")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "programacion/jornadas/{id}/modalidad")]
        HttpRequest req,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
