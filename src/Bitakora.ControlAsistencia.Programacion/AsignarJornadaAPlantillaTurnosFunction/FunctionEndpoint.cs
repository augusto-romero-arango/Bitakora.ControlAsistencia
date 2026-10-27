using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.AsignarJornadaAPlantillaTurnosFunction;

public class FunctionEndpoint
{
    private readonly IRequestValidator _requestValidator;
    private readonly ICommandRouter _commandRouter;

    public FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
    {
        _requestValidator = requestValidator;
        _commandRouter = commandRouter;
    }

    [Function("AsignarJornadaAPlantillaTurnos")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put",
            Route = "programacion/plantillas-turnos/{id}/jornada")]
        HttpRequest req,
        string id,
        CancellationToken ct)
        => throw new NotImplementedException();
}
