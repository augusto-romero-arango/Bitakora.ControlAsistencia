using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de CuadroSemanalTurnos (receta N1: stream de la plantilla).
/// partial es obligatorio (source generator de Marten).
/// </summary>
public sealed partial class CuadroSemanalTurnosProjection
    : SingleStreamProjection<CuadroSemanalTurnos, string>
{
    public static CuadroSemanalTurnos Create(IEvent<PlantillaSemanalCreada> e) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalAsignado e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalQuitado e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(TurnoDePlantillaSemanalSincronizado e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(JornadaDePlantillaSemanalAsignada e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(JornadaDePlantillaSemanalQuitada e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(
        LimitesDeJornadaDePlantillaSemanalSincronizados e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(
        AdvertenciasDePlantillaSemanalCalculadas e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static bool ShouldDelete(PlantillaSemanalRetirada e) => true;
}
