namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #115: Constantes de las fronteras horarias legales colombianas.
// Ley 2466/2025 art. 10: inicio jornada nocturna a las 7PM.
// CST art. 160: inicio jornada diurna a las 6AM.
// Consumidas desde el segmentador (#115) y el clasificador (#134).
public static class FronterasHorariasLegales
{
    public static readonly TimeOnly InicioNocturna = new(19, 0);
    public static readonly TimeOnly InicioDiurna = new(6, 0);
}
