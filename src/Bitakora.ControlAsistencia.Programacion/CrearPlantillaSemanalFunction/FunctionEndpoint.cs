using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

// Issue #620: primer endpoint del BC con el codigo de exito correcto (verificado por decompilacion
// de Cosmos.EventSourcing.CritterStack 2.3.1: InvokeAsync es inline, AutoApplyTransactions +
// UnitOfWorkMiddleware confirman la transaccion antes de responder). Regla del experto: Accepted
// solo cuando lo emitido fue un mensaje; Created si el objeto quedo persistido en el mismo POST.
// CA-5: validator invalido -> 400; router InvalidOperationException -> 409; router
// AggregateException -> 400 con mensajes; exito -> 201 Created con Location. Nunca AcceptedResult.
public class FunctionEndpoint(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function("CrearPlantillaSemanal")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/plantillas-semanales")]
        HttpRequest req,
        CancellationToken ct)
        => throw new NotImplementedException();
}
