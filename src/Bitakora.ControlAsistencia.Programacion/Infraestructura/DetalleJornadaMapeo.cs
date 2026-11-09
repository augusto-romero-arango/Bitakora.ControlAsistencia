using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

internal static class DetalleJornadaMapeo
{
    internal static DetalleJornada Mapear(JornadaProgramada jornada) =>
        new(jornada.JornadaId,
            jornada.Limites.HorasSemanales.TotalMinutos(),
            jornada.Limites.TopeDiario.TotalMinutos(),
            jornada.Limites.MinimoDiario.TotalMinutos(),
            jornada.Limites.DiasDescansoPorSemana,
            jornada.Modalidad?.Nombre,
            jornada.DescansoAcumulado,
            jornada.Ficha?.TopeDiarioEnMinutos,
            jornada.Ficha?.MinimoDiarioEnMinutos,
            jornada.Ficha?.HorasSemanalesEnMinutos);
}
