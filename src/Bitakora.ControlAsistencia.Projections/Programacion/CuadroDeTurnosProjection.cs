using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>Clase de proyeccion companion de CuadroDeTurnos (receta N1).</summary>
public sealed partial class CuadroDeTurnosProjection
    : SingleStreamProjection<CuadroDeTurnos, string>
{
    public static CuadroDeTurnos Create(IEvent<PlantillaDeTurnosCreada> e) =>
        throw new NotImplementedException();

    public static CuadroDeTurnos Apply(DiaDePlantillaDeTurnosAsignado e, CuadroDeTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroDeTurnos Apply(JornadaDePlantillaDeTurnosAsignada e, CuadroDeTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroDeTurnos Apply(JornadaDePlantillaDeTurnosQuitada e, CuadroDeTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroDeTurnos Apply(AdvertenciasDePlantillaDeTurnosCalculadas e, CuadroDeTurnos vista) =>
        throw new NotImplementedException();

    public static bool ShouldDelete(PlantillaDeTurnosRetirada e) =>
        throw new NotImplementedException();
}
