using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Entities;

public sealed record DiaDePlantillaAuditado(int Semana, DiaSemana Dia, Turno Turno, bool Retirado);

public static class AuditoriaPlantillaSemanal
{
    public static IReadOnlyList<AdvertenciaPlantillaSemanal> Auditar(
        int semanas,
        IEnumerable<DiaDePlantillaAuditado> diasOcupados,
        LimitesJornada? limites) => throw new NotImplementedException();
}
