using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de CuadroSemanalTurnos (receta N1: un solo stream, el de la
/// plantilla semanal PlantillaSemanalTurnos; MEF-ADR-0035, issue #624).
/// </summary>
/// <remarks>
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio pero falla en RUNTIME al
/// registrar la proyeccion (InvalidProjectionException).
///
/// Create toma IEvent&lt;PlantillaSemanalCreada&gt;, no PlantillaSemanalCreada a secas: la
/// identidad del documento es exactamente el StreamKey del stream de la plantilla, nunca
/// recomputada a mano desde el payload.
///
/// El retiro BORRA el cuadro (CA-ADR-0034 decision 4): el nombre queda libre para #626 y la
/// auditoria vive en el event store.
///
/// Implementacion pendiente de projection-implementer: los cuerpos son stub deliberado
/// (fase roja, projection-test-writer nunca implementa).
/// </remarks>
public sealed partial class CuadroSemanalTurnosProjection
    : SingleStreamProjection<CuadroSemanalTurnos, string>
{
    public static CuadroSemanalTurnos Create(IEvent<PlantillaSemanalCreada> e) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalAsignado e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static CuadroSemanalTurnos Apply(DiaDePlantillaSemanalQuitado e, CuadroSemanalTurnos vista) =>
        throw new NotImplementedException();

    public static bool ShouldDelete(PlantillaSemanalRetirada e) => throw new NotImplementedException();
}
