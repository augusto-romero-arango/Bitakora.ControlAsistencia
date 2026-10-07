using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler;

public partial class AsignarTurnoADiaDePlantillaSemanalCommandHandler
    : ICommandHandlerAsync<AsignarTurnoADiaDePlantillaSemanal>
{
    private readonly IEventStore _eventStore;

    public AsignarTurnoADiaDePlantillaSemanalCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarTurnoADiaDePlantillaSemanal command, CancellationToken ct = default)
    {
        var plantilla = await _eventStore.GetAggregateRootAsync<PlantillaSemanalTurnos>(
            command.PlantillaId, ct);
        if (plantilla is null)
            throw new RecursoNoEncontradoException(Mensajes.PlantillaNoEncontrada);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        // Guarda transaccional contra el aggregate ya cargado (Tell-don't-Ask, MEF-ADR-0012): un
        // turno solo es asignable a un dia de la plantilla si esta activo y completo (CA-ADR-0033).
        switch (catalogo.EvaluarAsignabilidad())
        {
            case ResultadoAsignabilidadTurno.Retirado:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoRetirado);
            case ResultadoAsignabilidadTurno.Incompleto:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.TurnoIncompleto);
        }

        var resultado = plantilla.AsignarDia(
            command.Semana, command.Dia, command.TurnoId, catalogo.Turno, catalogo.Version);
        switch (resultado)
        {
            case ResultadoAsignarDia.PlantillaRetirada:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.PlantillaRetirada);
            case ResultadoAsignarDia.SemanaFueraDeRango:
                throw new ReglaDeNegocioDeclinadaException(Mensajes.SemanaFueraDeRango);
        }
    }
}
