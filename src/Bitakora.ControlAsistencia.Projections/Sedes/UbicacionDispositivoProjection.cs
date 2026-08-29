using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui, NO en Marten.Events.Aggregation

namespace Bitakora.ControlAsistencia.Projections.Sedes;

/// <summary>
/// Clase de proyeccion companion de UbicacionDispositivo (receta N2, MEF-ADR-0035): el mismo
/// dispositivo puede aparecer instalado en streams de VARIAS sedes, asi que la identidad del
/// documento es el DispositivoId del payload, no el StreamKey de origen.
/// </summary>
/// <remarks>
/// partial es obligatorio: el source generator descubre Create/Apply/ShouldDelete por convencion y
/// emite el dispatcher [GeneratedEvolver]. Sin partial el build queda limpio y falla en RUNTIME al
/// registrar la proyeccion (InvalidProjectionException).
///
/// Semantica "solo la vigente": la sede vigente sale siempre del StreamKey del evento, nunca del
/// payload (ninguno de los dos eventos la lleva). No declarar ningun Create(DispositivoRetirado):
/// esa ausencia es lo que hace que un retiro sin documento existente no cree nada.
/// </remarks>
public sealed partial class UbicacionDispositivoProjection : MultiStreamProjection<UbicacionDispositivo, string>
{
    public UbicacionDispositivoProjection()
    {
        Identity<DispositivoInstalado>(e => e.DispositivoId);
        Identity<DispositivoRetirado>(e => e.DispositivoId);
    }

    public static UbicacionDispositivo Create(IEvent<DispositivoInstalado> e) =>
        new(e.Data.DispositivoId, e.StreamKey!);

    // El ultimo DispositivoInstalado aplicado reemplaza la sede vigente, aun si llega desde el
    // stream de una sede distinta: sin merge y sin conservar la anterior.
    public static UbicacionDispositivo Apply(IEvent<DispositivoInstalado> e, UbicacionDispositivo view) =>
        view with { SedeId = e.StreamKey! };

    // El retiro de la sede vigente elimina el documento SIN fallback a instalaciones anteriores no
    // retiradas; el retiro desde otra sede es limpieza de una instalacion fantasma y se ignora.
    public static bool ShouldDelete(IEvent<DispositivoRetirado> e, UbicacionDispositivo view) =>
        e.StreamKey == view.SedeId;
}
