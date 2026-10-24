using Cosmos.EventSourcing.Abstractions.Commands;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CrearTurnoDeHorarioAbierto;

namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction.CommandHandler;

public partial class CrearTurnoDeHorarioAbiertoCommandHandler : ICommandHandlerAsync<Comando>
{
    private readonly IEventStore _eventStore;
    private readonly ILectorNombresTurno _lectorNombres;

    public CrearTurnoDeHorarioAbiertoCommandHandler(IEventStore eventStore, ILectorNombresTurno lectorNombres)
    {
        _eventStore = eventStore;
        _lectorNombres = lectorNombres;
    }

    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
