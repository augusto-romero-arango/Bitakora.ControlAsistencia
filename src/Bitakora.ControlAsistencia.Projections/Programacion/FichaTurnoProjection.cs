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
/// partial es obligatorio: sin el, el source generator no emite el evolver y la proyeccion falla en
/// RUNTIME al registrarse (InvalidProjectionException), con el build limpio.
///
/// Create toma IEvent&lt;TurnoCreado&gt; porque el Id es el StreamKey, nunca recomputado desde el
/// payload.
///
/// El retiro BORRA la ficha, no la marca: la auditoria vive en el event store y el nombre debe
/// quedar libre para "modificar = retirar + crear" y para la invariante de nombre unico del catalogo.
/// </remarks>
public sealed partial class FichaTurnoProjection : SingleStreamProjection<FichaTurno, string>
{
    public static FichaTurno Create(IEvent<TurnoCreado> e)
    {
        var turnoCreado = e.Data;

        // EsDescanso es la UNICA frontera entre descanso y turno: un turno recien nacido tambien tiene
        // cero franjas (CA-ADR-0033), asi que el conteo lo confundiria con uno incompleto.
        if (turnoCreado.EsDescanso)
            return new FichaTurno(e.StreamKey!, turnoCreado.Nombre, true, "Descanso", [], "Descanso", Completo: true);

        // FranjaProgramada.Descripcion ya es el texto de FranjaOrdinaria.ToString(): se reusa en vez
        // de recomponerlo.
        var franjas = turnoCreado.FranjasOrdinarias.Select(f => MapearFranja(f.ToDetalle()));

        var horarioAbierto = turnoCreado.EsHorarioAbierto
            ? new HorarioAbiertoFicha(FormatearDuracion(turnoCreado.FranjasOrdinarias.Sum(f => f.MinutosOrdinarios())))
            : null;

        return Reconstruir(e.StreamKey!, turnoCreado.Nombre, esDescanso: false, franjas, horarioAbierto: horarioAbierto);
    }

    // Los eventos de diseno (CA-ADR-0033) traen la franja contenedora RESULTANTE, no el delta: por eso
    // todos reemplazan por HoraInicio en vez de mutar la franja materializada.

    public static FichaTurno Apply(FranjaAgregada e, FichaTurno ficha) =>
        ReemplazarOAgregar(ficha, e.Franja.ToDetalle());

    public static FichaTurno Apply(FranjaQuitada e, FichaTurno ficha)
    {
        var horaInicio = e.Franja.ToDetalle().HoraInicio;
        return Reconstruir(ficha, ficha.Franjas.Where(f => f.HoraInicio != horaInicio));
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

    public static FichaTurno Apply(VentanaDeEntradaModificada e, FichaTurno ficha) =>
        ficha with
        {
            VentanaDeEntrada = AbrirVentana(
                ficha.Franjas, TimeSpan.FromMinutes(e.Amplitud.TotalMinutos()), e.Amplitud.ToString())
        };

    public static bool ShouldDelete(TurnoRetirado e) => true;

    // Si ninguna franja coincide por HoraInicio, la agrega en vez de fallar: Apply no lanza
    // (MEF-ADR-0004).
    private static FichaTurno ReemplazarOAgregar(FichaTurno ficha, FranjaProgramada detalle) =>
        Reconstruir(ficha, ficha.Franjas
            .Where(f => f.HoraInicio != detalle.HoraInicio)
            .Append(MapearFranja(detalle)));

    private static FichaTurno Reconstruir(FichaTurno ficha, IEnumerable<FranjaFicha> franjas) =>
        Reconstruir(ficha.Id, ficha.Nombre, ficha.EsDescanso, franjas, ficha.VentanaDeEntrada, ficha.HorarioAbierto);

    // Unico punto de recomputo de HorarioResumido, Descripcion y Completo, para que no diverjan entre
    // Create y los Apply. Franjas ordenadas por HoraInicio, no por orden de diseno.
    private static FichaTurno Reconstruir(
        string id, string nombre, bool esDescanso, IEnumerable<FranjaFicha> franjas,
        VentanaDeEntradaFicha? ventana = null, HorarioAbiertoFicha? horarioAbierto = null)
    {
        var ordenadas = franjas.OrderBy(f => f.HoraInicio).ToList();

        if (ordenadas.Count == 0)
            return new FichaTurno(id, nombre, esDescanso, "Sin franjas", [], "Sin franjas", Completo: false);

        var horarioResumido = string.Join(", ",
            ordenadas.Select(f => $"{f.HoraInicio:HH\\:mm}-{f.HoraFin:HH\\:mm}"));
        var descripcion = string.Join(", ", ordenadas.Select(f => f.Descripcion));

        // La vista no guarda la amplitud en minutos: se recupera como Cierre - Apertura, asi que al
        // mover el primer inicio la ventana se desplaza conservando amplitud y texto.
        var ventanaVigente = ventana is null
            ? null
            : AbrirVentana(ordenadas, ventana.Cierre - ventana.Apertura, ventana.Amplitud);

        return new FichaTurno(id, nombre, esDescanso, horarioResumido, ordenadas, descripcion, Completo: true, ventanaVigente, horarioAbierto);
    }

    // Unico punto de calculo de la ventana. Amplitud 0 = turno fijo; sin franjas no hay apertura y
    // la ventana queda null sin lanzar (MEF-ADR-0004).
    private static VentanaDeEntradaFicha? AbrirVentana(
        IReadOnlyList<FranjaFicha> franjas, TimeSpan amplitud, string textoAmplitud)
    {
        if (amplitud <= TimeSpan.Zero || franjas.Count == 0)
            return null;

        var apertura = franjas.Min(f => f.HoraInicio);
        return new VentanaDeEntradaFicha(apertura, apertura.Add(amplitud), textoAmplitud);
    }

    private static string FormatearDuracion(int minutos) =>
        HorasYMinutos.Crear(minutos / 60, minutos % 60).ToString();

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
