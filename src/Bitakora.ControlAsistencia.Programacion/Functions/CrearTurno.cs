using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.Dominio.Comandos.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.Functions;

// HU-4: Endpoint HTTP POST para crear un turno de trabajo
// ADR-0008: [Function(nameof(CrearTurno))] como convencion de nombrado
// Flujo: validar request -> despachar comando -> retornar 202 o error
// ADR-0007: InvalidOperationException -> 409 Conflict
//           AggregateException (del factory) -> 400 Bad Request con mensajes
public class CrearTurno(IRequestValidator requestValidator, ICommandRouter commandRouter)
{
    [Function(nameof(CrearTurno))]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "programacion/turnos")]
        HttpRequest req,
        CancellationToken ct)
        => throw new NotImplementedException();
}
