using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoCrearTurno = Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CrearTurno;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.CommandHandler;

// HU-4: Handler que crea un nuevo turno de trabajo
// Flujo: verificar idempotencia -> construir evento via TurnoCreado.Crear(comando)
//        -> iniciar stream del aggregate -> persistir
// ADR-0007: lanza InvalidOperationException si el turno ya existe (-> 409 Conflict)
//           deja propagar AggregateException del factory (-> 400 Bad Request)
public partial class CrearTurnoCommandHandler : ICommandHandlerAsync<ComandoCrearTurno>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresTurno _lectorNombres;

    public CrearTurnoCommandHandler(IEventStore eventStore, ILectorNombresTurno lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public async Task HandleAsync(ComandoCrearTurno command, CancellationToken ct = default)
    {
        var existe = await _eventStore.ExistsAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.TurnoYaExiste);

        // Issue #497: pendiente el rechazo por nombre normalizado duplicado contra FichaTurno
        // (via _lectorNombres) -- fase roja, implementacion en la fase verde.

        var evento = command.EsDescanso
            ? TurnoCreado.CrearDescanso(command.TurnoId, command.Nombre)
            : TurnoCreado.Crear(command.TurnoId, command.Nombre, command.ToDatosFranjas());
        var catalogo = CatalogoTurnos.Iniciar(evento);
        _eventStore.StartStream(catalogo);
    }
}
