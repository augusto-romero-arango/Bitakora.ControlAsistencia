using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public sealed record DiaDePlantillaTurnosAuditado(int Dia, Turno Turno);

public static class AuditoriaPlantillaTurnos
{
    public static IReadOnlyList<AdvertenciaPlantillaTurnos> Auditar(
        int diasDelCiclo,
        IEnumerable<DiaDePlantillaTurnosAuditado> diasOcupados,
        LimitesJornada? limites) =>
        throw new NotImplementedException();
}
