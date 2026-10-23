using Bitakora.ControlAsistencia.ControlHoras.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ResolverAmbiguedadDeMarcacionesFunction;

public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("ResolverAmbiguedadDeMarcaciones")]
    public Task<IActionResult> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "control-horas/depuraciones/{codigoColaborador}/{fecha}:resolver-ambiguedad")]
        HttpRequest req,
        string codigoColaborador,
        string fecha,
        CancellationToken ct)
        => throw new NotImplementedException();
}
