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

        // Cero franjas ordinarias es la variante descanso (TurnoCreado.CrearDescanso).
        if (turnoCreado.FranjasOrdinarias.Count == 0)
            return new FichaTurno(e.StreamKey!, turnoCreado.Nombre, true, "Descanso", [], "Descanso");

        // ToDetalle() es el DTO plano que el VO rico ya expone (Tell-don't-Ask, MEF-ADR-0012): sus
        // campos son privados, no se reabren aqui. FranjaProgramada.Descripcion ya es el texto que
        // FranjaOrdinaria.ToString() produce, asi que se reusa en vez de recomponerlo.
        var detalles = turnoCreado.FranjasOrdinarias.Select(f => f.ToDetalle()).ToList();

        var horarioResumido = string.Join(", ",
            detalles.Select(d => $"{d.HoraInicio:HH\\:mm}-{d.HoraFin:HH\\:mm}"));
        var descripcion = string.Join(", ", detalles.Select(d => d.Descripcion));

        return new FichaTurno(
            e.StreamKey!,
            turnoCreado.Nombre,
            false,
            horarioResumido,
            detalles.Select(MapearFranja).ToList(),
            descripcion);
    }

    public static bool ShouldDelete(TurnoRetirado e) => true;

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
