using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction;

public record SincronizarTurnoDePlantillaSemanal(
    Guid PlantillaId, Guid TurnoId, Turno Turno, long Version, bool Retirado);
