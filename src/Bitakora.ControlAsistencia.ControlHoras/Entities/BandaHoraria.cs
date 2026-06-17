// Issue #134: Clasificar segmentos horarios por banda y tipo de dia
// Referencia legal: CST art. 160 (inicio jornada diurna 6AM) + Ley 2466/2025 art. 10
// (inicio jornada nocturna 7PM). Los umbrales concretos viven en FronterasHorariasLegales.
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
