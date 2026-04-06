using Bitakora.ControlAsistencia.Programacion.Dominio.Entities;
using Bitakora.ControlAsistencia.Programacion.Dominio.Eventos;
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

    public async Task HandleAsync(ComandoCrearTurno command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.TurnoYaExiste);

        var evento = TurnoCreado.Crear(command);
        var catalogo = CatalogoTurnos.Iniciar(evento);
        _eventStore.StartStream(catalogo);
    }
}
