// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
// Referencia legal: art. 160 CST - jornada diurna 6AM a 10PM (limite practico 7PM segun convencion)
namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Banda horaria de un segmento de trabajo segun legislacion laboral colombiana.
/// </summary>
public enum BandaHoraria
{
    /// <summary>Banda entre las 06:00 y las 19:00.</summary>
    Diurna,

    /// <summary>Banda entre las 19:00 y las 06:00 del dia siguiente.</summary>
    Nocturna
}
