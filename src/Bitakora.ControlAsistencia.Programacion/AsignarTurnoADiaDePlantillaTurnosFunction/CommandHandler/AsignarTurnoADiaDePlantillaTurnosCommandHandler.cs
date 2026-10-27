using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaTurnosFunction.CommandHandler;

public partial class AsignarTurnoADiaDePlantillaTurnosCommandHandler
    : ICommandHandlerAsync<AsignarTurnoADiaDePlantillaTurnos>
{
    private readonly IEventStore _eventStore;

    public AsignarTurnoADiaDePlantillaTurnosCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarTurnoADiaDePlantillaTurnos command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaTurnos>(command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        switch (catalogo.EvaluarAsignabilidad())
        {
            case ResultadoAsignabilidadTurno.Retirado:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoRetirado);
            case ResultadoAsignabilidadTurno.Incompleto:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoIncompleto);
        }

        var resultado = plantilla.AsignarDia(command.Dia, command.TurnoId, catalogo.Turno, catalogo.Version);
        if (resultado == ResultadoAsignarDiaDePlantillaTurnos.DiaFueraDelCiclo)
            throw new ReglaDeNegocioDeclinadaException(Mensajes.DiaFueraDelCiclo);
    }
}
