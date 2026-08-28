using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Sedes;

/// <summary>
/// Clase de proyeccion companion de FichaSede (issue #461, receta N1 -- un solo stream, el de la
/// sede; MEF-ADR-0035). Vive en el worker (Bitakora.ControlAsistencia.Projections), el ensamblado
/// que si referencia Marten y el analizador JasperFx.Events.SourceGenerator.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial el build queda
/// limpio pero falla en RUNTIME al registrar la proyeccion (InvalidProjectionException).
///
/// Se registra en ConfiguracionMartenProjectionsSedes.ConfigurarSedes con
/// opts.Projections.Add&lt;FichaSedeProjection&gt;(ProjectionLifecycle.Async) -- AUSENTE hoy a
/// proposito: el seam existe desde el issue #455 sin ninguna proyeccion concreta, y sumar esa unica
/// linea es responsabilidad de projection-implementer (fase verde). Hasta entonces
/// ConfiguracionMartenProjectionsTests.ConfigurarSedes_RegistraFichaSedeProjectionComoAsync queda en
/// rojo.
///
/// Create toma IEvent&lt;SedeRegistrada&gt;, no SedeRegistrada a secas: la identidad del documento
/// (FichaSede.Id) es exactamente el StreamKey del stream de SedeAggregateRoot (Events.StreamIdentity
/// = AsString) -- IEvent&lt;T&gt;.StreamKey es quien la expone, sin recomputarla a mano desde el
/// payload (mismo criterio que skills/projections/modelos-marten.md).
///
/// Sin ShouldDelete: la ficha nunca se borra (issue #461, "Receta" no lo pide).
///
/// Cuerpos en NotImplementedException (MEF-ADR-0033, stub minimo de compilacion): el COMPORTAMIENTO
/// (que campo actualiza cada evento) es responsabilidad de projection-implementer; los oraculos ya
/// estan fijados por FichaSedeProjectionTests (Projections.Tests).
/// </summary>
public sealed partial class FichaSedeProjection : SingleStreamProjection<FichaSede, string>
{
    public static FichaSede Create(IEvent<SedeRegistrada> e) => throw new NotImplementedException();

    public static FichaSede Apply(NombreSedeModificado e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(UbicacionActualizada e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(CentroDeCostosAsignado e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(CentroDeCostosRetirado e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(SedeActivada e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(SedeDesactivada e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(DispositivoInstalado e, FichaSede vista) => throw new NotImplementedException();

    public static FichaSede Apply(DispositivoRetirado e, FichaSede vista) => throw new NotImplementedException();
}
