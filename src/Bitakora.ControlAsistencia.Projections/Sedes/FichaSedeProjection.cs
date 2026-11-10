using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Sedes;

/// <summary>
/// partial es obligatorio: sin el, el source generator no emite el evolver y la proyeccion falla en
/// RUNTIME al registrarse (InvalidProjectionException), con el build limpio.
///
/// Create toma IEvent&lt;SedeRegistrada&gt; porque FichaSede.Id es el StreamKey, nunca recomputado
/// desde el payload. Sin ShouldDelete: la ficha nunca se borra.
/// </summary>
public sealed partial class FichaSedeProjection : SingleStreamProjection<FichaSede, string>
{
    // Activa nace true, sin CC ni dispositivos.
    public static FichaSede Create(IEvent<SedeRegistrada> e) =>
        new(e.StreamKey!, e.Data.Codigo, e.Data.Nombre, e.Data.Ciudad, e.Data.Direccion, null, true, []);

    public static FichaSede Apply(NombreSedeModificado e, FichaSede vista) =>
        vista with { Nombre = e.Nombre };

    public static FichaSede Apply(UbicacionActualizada e, FichaSede vista) =>
        vista with { Ciudad = e.Ciudad, Direccion = e.Direccion };

    public static FichaSede Apply(CentroDeCostosAsignado e, FichaSede vista) =>
        vista with { CentroDeCostos = e.CentroDeCostos };

    public static FichaSede Apply(CentroDeCostosRetirado e, FichaSede vista) =>
        vista with { CentroDeCostos = null };

    public static FichaSede Apply(SedeActivada e, FichaSede vista) =>
        vista with { Activa = true };

    public static FichaSede Apply(SedeDesactivada e, FichaSede vista) =>
        vista with { Activa = false };

    public static FichaSede Apply(DispositivoInstalado e, FichaSede vista) =>
        vista with { Dispositivos = [.. vista.Dispositivos, e.DispositivoId] };

    public static FichaSede Apply(DispositivoRetirado e, FichaSede vista) =>
        vista with { Dispositivos = [.. vista.Dispositivos.Where(d => d != e.DispositivoId)] };
}
