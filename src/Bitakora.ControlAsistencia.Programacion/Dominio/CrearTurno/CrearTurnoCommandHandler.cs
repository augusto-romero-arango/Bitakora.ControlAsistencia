using Cosmos.EventSourcing.Abstractions.Commands;

using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.Dominio.Comandos.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.Dominio.CrearTurno;

// HU-4: Handler que crea un nuevo turno de trabajo
// Flujo: verificar idempotencia -> construir evento via TurnoCreado.Crear(comando)
//        -> iniciar stream del aggregate -> persistir
// ADR-0007: lanza InvalidOperationException si el turno ya existe (-> 409 Conflict)
//           deja propagar AggregateException del factory (-> 400 Bad Request)
public partial class CrearTurnoCommandHandler : ICommandHandlerAsync<ComandoCrearTurno>
{
    private readonly IEventStore _eventStore;

    public CrearTurnoCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ComandoCrearTurno command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
