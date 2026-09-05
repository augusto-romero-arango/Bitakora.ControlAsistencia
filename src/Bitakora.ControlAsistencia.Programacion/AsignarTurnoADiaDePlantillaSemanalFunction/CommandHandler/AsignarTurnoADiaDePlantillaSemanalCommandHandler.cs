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

    public Task HandleAsync(AsignarTurnoADiaDePlantillaSemanal command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
