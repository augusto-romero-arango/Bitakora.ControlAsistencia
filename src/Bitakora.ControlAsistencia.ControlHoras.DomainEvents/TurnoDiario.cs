namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Representacion plana del turno que rige efectivamente a un empleado en una fecha concreta,
/// propia de este ensamblado (issue #322, payload por rol -- CA-ADR-0029 decision #5 /
/// MEF-ADR-0039 decision #6). Duplica con paridad exacta de campos a DetalleTurno
/// (PrivateEvents.Programacion) en vez de importarlo: los tres ensamblados de eventos son tres
/// islas sin referencias entre si. Nombre acordado con el usuario (docs/eda/ubiquitous-language.yaml):
/// "TurnoDiario" es el termino exacto del glosario para este concepto.
/// </summary>
/// <remarks>
/// Equals/GetHashCode propios comparan FranjasOrdinarias POR VALOR (SequenceEqual) y EXCLUYEN
/// Descripcion (dato derivado, no identidad del turno) -- mismo criterio que DetalleTurno
/// (issue #288) y TurnoProgramado (Programacion.DomainEvents, issue #319).
/// </remarks>
public record TurnoDiario(
    string Nombre,
    IReadOnlyList<FranjaProgramada> FranjasOrdinarias,
    string Descripcion)
{
    /// <summary>
    /// Los eventos anteriores a este record no llevan el campo: STJ deja null en el parametro
    /// posicional y la propiedad declarada como string no anulable mentiria. Se normaliza a
    /// cadena vacia -- mismo criterio que DetalleTurno (issue #288).
    /// </summary>
    public string Descripcion { get; init; } = Descripcion ?? string.Empty;

    public virtual bool Equals(TurnoDiario? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Nombre == other.Nombre
            && FranjasOrdinarias.SequenceEqual(other.FranjasOrdinarias);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Nombre);
        foreach (var franja in FranjasOrdinarias) hash.Add(franja);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Segmenta el turno en bloques absolutos de tiempo (issue #327): resuelve los offsets de dia de
    /// cada franja/descanso/extra contra <paramref name="fecha"/> (el dia de asignacion del turno) y
    /// rompe en la medianoche cualquier bloque que cruce el limite del dia. Lectura pura -- no muta
    /// el turno ni persiste el resultado (MEF-ADR-0012, Tell-don't-Ask: la aritmetica vive junto al
    /// dato, no como calculo externo sobre TimeOnly/DiaOffset crudos).
    /// </summary>
    public IReadOnlyList<BloqueTurno> Segmentar(DateOnly fecha) =>
        FranjasOrdinarias
            .SelectMany(SegmentarFranja)
            .SelectMany(RomperEnMedianoche)
            .Select(tramo => new BloqueTurno(
                tramo.Tipo, ResolverMomento(fecha, tramo.Inicio), ResolverMomento(fecha, tramo.Fin)))
            .ToList();

    private const int MinutosPorHora = 60;
    private const int MinutosPorDia = 1440;

    /// <summary>
    /// Tramo intermedio en minutos absolutos desde la medianoche de la fecha ancla -- misma
    /// aritmetica que <c>MomentoDelDia.MinutosAbsolutos</c> (ControlHoras/ValueObjects), pero
    /// autonoma: este ensamblado no puede referenciar el Function App que lo consume
    /// (MEF-ADR-0039 decisiones 2/4, isla de eventos).
    /// </summary>
    private readonly record struct Tramo(TipoBloque Tipo, int Inicio, int Fin);

    private static int MinutosAbsolutos(TimeOnly hora, int diaOffset) =>
        hora.Hour * MinutosPorHora + hora.Minute + diaOffset * MinutosPorDia;

    /// <summary>
    /// Recorta la franja ordinaria por sus descansos y extras (glosario: ambos contenidos en la
    /// ordinaria, tratados igual -- issue #327 notas tecnicas): produce tramos Ordinaria en los
    /// huecos y tramos Descanso/Extra en cada sub-franja, en orden cronologico.
    /// </summary>
    private static IEnumerable<Tramo> SegmentarFranja(FranjaProgramada franja)
    {
        var inicioFranja = MinutosAbsolutos(franja.HoraInicio, 0);
        var finFranja = MinutosAbsolutos(franja.HoraFin, franja.DiaOffsetFin);

        var subFranjas = franja.Descansos
            .Select(d => new Tramo(TipoBloque.Descanso,
                MinutosAbsolutos(d.HoraInicio, d.DiaOffsetInicio), MinutosAbsolutos(d.HoraFin, d.DiaOffsetFin)))
            .Concat(franja.Extras
                .Select(e => new Tramo(TipoBloque.Extra,
                    MinutosAbsolutos(e.HoraInicio, e.DiaOffsetInicio), MinutosAbsolutos(e.HoraFin, e.DiaOffsetFin))))
            .OrderBy(sub => sub.Inicio)
            .ToList();

        var cursor = inicioFranja;
        foreach (var sub in subFranjas)
        {
            if (sub.Inicio > cursor)
                yield return new Tramo(TipoBloque.Ordinaria, cursor, sub.Inicio);
            yield return sub;
            cursor = sub.Fin;
        }

        if (cursor < finFranja)
            yield return new Tramo(TipoBloque.Ordinaria, cursor, finFranja);
    }

    /// <summary>
    /// Rompe un tramo en cada medianoche que cruce (CA-4): ningun bloque resultante abarca mas
    /// de un dia calendario.
    /// </summary>
    private static IEnumerable<Tramo> RomperEnMedianoche(Tramo tramo)
    {
        var cursor = tramo.Inicio;
        var primerLimite = (cursor / MinutosPorDia + 1) * MinutosPorDia;
        for (var limite = primerLimite; limite < tramo.Fin; limite += MinutosPorDia)
        {
            yield return new Tramo(tramo.Tipo, cursor, limite);
            cursor = limite;
        }

        yield return new Tramo(tramo.Tipo, cursor, tramo.Fin);
    }

    private static DateTime ResolverMomento(DateOnly fecha, int minutosAbsolutos)
    {
        var dias = minutosAbsolutos / MinutosPorDia;
        var minutosDelDia = minutosAbsolutos % MinutosPorDia;
        return fecha.AddDays(dias).ToDateTime(TimeOnly.MinValue).AddMinutes(minutosDelDia);
    }
}
