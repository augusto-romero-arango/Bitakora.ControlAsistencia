using Bitakora.ControlAsistencia.ReadModels.Sedes;
using Bitakora.ControlAsistencia.Sedes.DomainEvents;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Projections; // MultiStreamProjection<,> vive aqui, NO en Marten.Events.Aggregation

namespace Bitakora.ControlAsistencia.Projections.Sedes;

/// <summary>
/// Clase de proyeccion companion de UbicacionDispositivo (issue #475, receta N2 --
/// MultiStreamProjection, MEF-ADR-0035): el mismo dispositivo puede aparecer instalado en streams
/// de VARIAS sedes distintas, asi que la correlacion no es "un solo stream" (N1) -- la identidad
/// del documento es DispositivoId, un campo del payload, no el StreamKey de origen.
///
/// partial es obligatorio (skills/projections/modelos-marten.md): el source generator descubre
/// Create/Apply/ShouldDelete por convencion y emite el dispatcher [GeneratedEvolver]. Sin partial
/// el build queda limpio pero falla en RUNTIME al registrar la proyeccion
/// (InvalidProjectionException).
///
/// Semantica "solo la vigente" (decidida punto a punto con el experto, sesion 2026-08-29):
/// - DispositivoInstalado (Create/Apply): la sede sale de IEvent.StreamKey, nunca del payload --
///   el ultimo evento aplicado gana, reemplazando la sede previa sin merge.
/// - DispositivoRetirado de la sede VIGENTE (ShouldDelete == true): el documento se elimina, SIN
///   fallback a instalaciones anteriores no retiradas -- si el maestro retiro el dispositivo de
///   donde realmente estaba, la instalacion fantasma previa era el error.
/// - DispositivoRetirado de una sede DISTINTA a la vigente (ShouldDelete == false): se ignora,
///   documento intacto (limpieza de una instalacion fantasma).
/// - DispositivoRetirado sin documento existente: se ignora estructuralmente -- esta clase no
///   declara ningun Create(DispositivoRetirado), asi que Marten no tiene metodo que despachar y
///   nunca crea el documento (mismo patron que CategoriaDeEtiquetasProjection, issue #357, CA-5:
///   sin test dedicado -- la garantia es la ausencia del metodo, no comportamiento a ejercer).
///
/// Se registra en ConfiguracionMartenProjectionsSedes.ConfigurarSedes junto a FichaSedeProjection
/// con opts.Projections.Add&lt;UbicacionDispositivoProjection&gt;(ProjectionLifecycle.Async) --
/// registro pendiente (fase roja de este issue; projection-implementer lo agrega).
/// </summary>
public sealed partial class UbicacionDispositivoProjection : MultiStreamProjection<UbicacionDispositivo, string>
{
    public UbicacionDispositivoProjection()
    {
        Identity<DispositivoInstalado>(e => e.DispositivoId);
        Identity<DispositivoRetirado>(e => e.DispositivoId);
    }

    // CA-1: Id = DispositivoId (payload); SedeId = StreamKey del stream de origen (envolvente del
    // evento, nunca recomputado a mano desde el payload).
    public static UbicacionDispositivo Create(IEvent<DispositivoInstalado> e) =>
        new(e.Data.DispositivoId, e.StreamKey!);

    // CA-2: el ultimo DispositivoInstalado aplicado reemplaza la sede vigente, aun si llega desde
    // el stream de una sede distinta a la actual.
    public static UbicacionDispositivo Apply(IEvent<DispositivoInstalado> e, UbicacionDispositivo view) =>
        view with { SedeId = e.StreamKey! };

    // CA-3/CA-4: elimina SOLO si el retiro es de la sede vigente (e.StreamKey == view.SedeId); un
    // retiro de una sede distinta se ignora, documento intacto.
    public static bool ShouldDelete(IEvent<DispositivoRetirado> e, UbicacionDispositivo view) =>
        e.StreamKey == view.SedeId;
}
