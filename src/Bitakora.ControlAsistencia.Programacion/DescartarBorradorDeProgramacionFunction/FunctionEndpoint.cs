using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction;

public partial class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function(nameof(DescartarBorradorDeProgramacion))]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/colaboradores/{codigo}/borrador:descartar")]
        HttpRequest req,
        string codigo,
        CancellationToken ct)
        => throw new NotImplementedException();
}
