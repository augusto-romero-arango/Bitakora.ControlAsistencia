using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #115: Segmenta un IntervaloTemporal en sub-intervalos homogeneos cortando en las
// fronteras horarias legales (6AM, 7PM, medianoche). Logica pura sin estado.
// Diseno: trabaja sobre MomentoDelDia/IntervaloTemporal, nunca sobre DateTime.
// La conversion DateTime -> MomentoDelDia ocurre una sola vez en la frontera (#136).
public static class SegmentadorHorario
{
    // Corta el intervalo en cada ocurrencia de las fronteras 6AM, 7PM y medianoche
    // dentro del rango (exclusivo de los extremos). Si no hay fronteras: retorna [intervalo].
    public static IReadOnlyList<IntervaloTemporal> Segmentar(IntervaloTemporal intervalo)
        => throw new NotImplementedException();
}
