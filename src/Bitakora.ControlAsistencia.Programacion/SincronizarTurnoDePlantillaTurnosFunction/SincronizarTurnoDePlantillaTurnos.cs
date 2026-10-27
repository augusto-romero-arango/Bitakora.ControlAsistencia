using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction;

public record SincronizarTurnoDePlantillaTurnos(
    Guid PlantillaId, Guid TurnoId, Turno Turno, long Version, bool Retirado);
