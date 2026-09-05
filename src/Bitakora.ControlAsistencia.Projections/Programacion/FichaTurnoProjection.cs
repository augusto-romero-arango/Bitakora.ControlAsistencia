using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using JasperFx.Events; // IEvent<T> vive aqui, NO en Marten.Events (MEF-ADR-0034 seccion 6)
using Marten.Events.Aggregation; // SingleStreamProjection<,> vive aqui, NO en Marten.Events.Projections

namespace Bitakora.ControlAsistencia.Projections.Programacion;

/// <summary>
/// Clase de proyeccion companion de FichaTurno (receta N1: un solo stream, el del catalogo de
/// turnos CatalogoTurnos; MEF-ADR-0035).
/// </summary>
/// <remarks>
/// partial es obligatorio: el source generator descubre Create/Apply por convencion y emite el
/// dispatcher [GeneratedEvolver]. Sin partial el build queda limpio pero falla en RUNTIME al
/// registrar la proyeccion (InvalidProjectionException).
///
/// Registrarla en ConfiguracionMartenProjectionsProgramacion es lo que hace que Marten aplique
/// ProjectionDocumentPolicy sobre FichaTurno (mt_version bigint): el Function App que la consulta
/// debe declarar la misma forma con Schema.For&lt;FichaTurno&gt;().UseNumericRevisions(true).
///
/// Create toma IEvent&lt;TurnoCreado&gt;, no TurnoCreado a secas: la identidad del documento es
/// exactamente el StreamKey del stream de CatalogoTurnos (StreamIdentity = AsString), nunca
/// recomputada a mano desde el payload.
///
/// El retiro BORRA la ficha, no la marca: la auditoria vive en el event store y el nombre debe
/// quedar libre para el patron "modificar = retirar + crear" y para la invariante de nombre unico
/// del catalogo.
/// </remarks>
public sealed partial class FichaTurnoProjection : SingleStreamProjection<FichaTurno, string>
{
    public static FichaTurno Create(IEvent<TurnoCreado> e)
    {
        var turnoCreado = e.Data;

        // Cero franjas ordinarias: puede ser la variante descanso (TurnoCreado.CrearDescanso) o un
        // turno recien nacido, todavia incompleto (CA-ADR-0033) -- EsDescanso.Crear() es la unica
        // frontera entre ambos casos, nunca el conteo de franjas (issue #607, CA-1/CA-6).
        if (turnoCreado.FranjasOrdinarias.Count == 0)
        {
            return turnoCreado.EsDescanso
                ? new FichaTurno(e.StreamKey!, turnoCreado.Nombre, true, "Descanso", [], "Descanso", Completo: true)
                : Reconstruir(e.StreamKey!, turnoCreado.Nombre, false, []);
        }

        // ToDetalle() es el DTO plano que el VO rico ya expone (Tell-don't-Ask, MEF-ADR-0012): sus
        // campos son privados, no se reabren aqui. FranjaProgramada.Descripcion ya es el texto que
        // FranjaOrdinaria.ToString() produce, asi que se reusa en vez de recomponerlo.
        var detalles = turnoCreado.FranjasOrdinarias.Select(f => f.ToDetalle()).ToList();

        return Reconstruir(e.StreamKey!, turnoCreado.Nombre, false, detalles.Select(MapearFranja).ToList());
    }

    // Los 8 Apply de los eventos de diseno por pasos (CA-ADR-0033) reemplazan/agregan/quitan la
    // FranjaFicha cuyo HoraInicio coincide con evento.Franja.ToDetalle().HoraInicio (o la agregan
    // si no existe, MEF-ADR-0004: Apply no lanza) y recalculan HorarioResumido/Descripcion/Completo
    // en un unico punto (Reconstruir), con las franjas ordenadas por HoraInicio.

    public static FichaTurno Apply(FranjaAgregada e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(FranjaQuitada e, FichaTurno ficha)
    {
        var horaInicio = e.Franja.ToDetalle().HoraInicio;
        var franjas = ficha.Franjas.Where(f => f.HoraInicio != horaInicio).ToList();
        return Reconstruir(ficha.Id, ficha.Nombre, ficha.EsDescanso, franjas);
    }

    public static FichaTurno Apply(DescansoAgregado e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(ExtraAgregado e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(DescansoQuitado e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(ExtraQuitado e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(SedeDeFranjaAsignada e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(SedeDeFranjaRetirada e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static bool ShouldDelete(TurnoRetirado e) => true;

    // Reemplaza la FranjaFicha cuyo HoraInicio coincide con la del detalle recibido -- o la agrega
    // si ninguna coincide (MEF-ADR-0004: Apply no lanza) -- y recalcula los derivados.
    private static FichaTurno ReemplazarOAgregar(FichaTurno ficha, FranjaProgramada detalle)
    {
        var franjaNueva = MapearFranja(detalle);
        var franjas = ficha.Franjas.Where(f => f.HoraInicio != detalle.HoraInicio).Append(franjaNueva).ToList();
        return Reconstruir(ficha.Id, ficha.Nombre, ficha.EsDescanso, franjas);
    }

    // Unico punto de recomputo de los derivados (issue #607): evita que HorarioResumido,
    // Descripcion y Completo diverjan entre Create y los Apply. Las franjas se presentan ordenadas
    // por HoraInicio (vista para leer el dia, no el orden en que se diseno -- MEF-ADR-0041).
    private static FichaTurno Reconstruir(
        string id, string nombre, bool esDescanso, IReadOnlyList<FranjaFicha> franjas)
    {
        var ordenadas = franjas.OrderBy(f => f.HoraInicio).ToList();

        if (ordenadas.Count == 0)
            return new FichaTurno(id, nombre, esDescanso, "Sin franjas", [], "Sin franjas", Completo: false);

        var horarioResumido = string.Join(", ",
            ordenadas.Select(f => $"{f.HoraInicio:HH\\:mm}-{f.HoraFin:HH\\:mm}"));
        var descripcion = string.Join(", ", ordenadas.Select(f => f.Descripcion));

        return new FichaTurno(id, nombre, esDescanso, horarioResumido, ordenadas, descripcion, Completo: true);
    }

    private static FranjaFicha MapearFranja(FranjaProgramada detalle) =>
        new(
            detalle.HoraInicio,
            detalle.HoraFin,
            detalle.DiaOffsetFin,
            detalle.Descansos.Select(MapearSubFranja).ToList(),
            detalle.Extras.Select(MapearSubFranja).ToList(),
            detalle.Sede?.Id,
            detalle.Sede?.Nombre,
            detalle.Descripcion);

    private static SubFranjaFicha MapearSubFranja(SubFranjaProgramada detalle) =>
        new(detalle.HoraInicio, detalle.HoraFin, detalle.DiaOffsetInicio, detalle.DiaOffsetFin);
}
