using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;

// Alias temporal de la ruta anterior: el MCP se despliega aparte y sigue llamandola hasta #1387,
// que lo retira borrando solo este archivo.
public class FunctionEndpointRutaEnFechas(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("SolicitarProgramacionTurnoEnFechas")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/solicitudes-en-fechas")]
        HttpRequest req,
        CancellationToken ct) =>
        new FunctionEndpoint(requestValidator, commandRouter).SolicitarAsync(req, ct);
}
