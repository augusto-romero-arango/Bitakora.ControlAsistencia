using Bitakora.ControlAsistencia.Contracts.ControlHoras.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// Issue #116: Consolida los DesgloseFranja de un dia operativo en un DesgloseHoras,
// aplicando compensacion cronologica inversa cross-franja sobre las extras de todas
// las franjas. Clase estatica pura: sin interaccion de aggregate ni event sourcing.
//
// La integracion reactiva al aggregate (ControlDiarioAggregateRoot) se hace en #139.
//
// STUB de fase roja (#116): la implementacion real la escribe el implementer.
public static class ConsolidadorDesgloseHoras
{
    public static DesgloseHoras Consolidar(
        IReadOnlyList<DesgloseFranja> desglosesFranja,
        int franjasAnomalas) =>
        throw new NotImplementedException();
}
