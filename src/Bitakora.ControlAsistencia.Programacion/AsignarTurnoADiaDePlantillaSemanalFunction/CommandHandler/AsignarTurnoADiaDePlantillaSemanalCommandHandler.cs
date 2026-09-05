using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AsignarTurnoADiaDePlantillaSemanalFunction.CommandHandler;

// Issue #621: precondiciones de orquestacion (404 plantilla/turno) y de asignabilidad del turno
// (409 retirado/incompleto, mismo mecanismo que SolicitarProgramacionTurnoCommandHandler via
// CatalogoTurnos.EvaluarAsignabilidad) antes de delegar en PlantillaSemanalTurnos.AsignarDia, que
// declina con resultado (CA-ADR-0030) la regla de rango de semana / idempotencia. El BC no ha
// scaffoldeado aun RecursoYaExisteException/RecursoNoEncontradoException (regimen de coexistencia,
// MEF-ADR-0004) -- sigue el patron vigente del repo: KeyNotFoundException / InvalidOperationException.
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
            throw new KeyNotFoundException(Mensajes.PlantillaNoEncontrada);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        // Guarda transaccional contra el aggregate ya cargado (Tell-don't-Ask, MEF-ADR-0012): un
        // turno solo es asignable a un dia de la plantilla si esta activo y completo (CA-ADR-0033).
        switch (catalogo.EvaluarAsignabilidad())
        {
            case ResultadoAsignabilidadTurno.Retirado:
                throw new InvalidOperationException(Mensajes.TurnoRetirado);
            case ResultadoAsignabilidadTurno.Incompleto:
                throw new InvalidOperationException(Mensajes.TurnoIncompleto);
        }

        var resultado = plantilla.AsignarDia(command.Semana, command.Dia, command.TurnoId);
        if (resultado == ResultadoAsignarDia.SemanaFueraDeRango)
            throw new InvalidOperationException(Mensajes.SemanaFueraDeRango);
    }
}
