// Trabaja siempre en MomentoDelDia: no construye ningun DateTime en este nivel.
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Produce la lista de IntervaloClasificado que describe cada minuto trabajado con su concepto legal,
/// dado el contexto de una franja programada. La entrada efectiva es max(trabajado.Inicio, inicioFranja):
/// los minutos antes del inicio programado no aparecen en el desglose.
/// </summary>
public static class ClasificadorTrabajo
{
    // Cada segmento queda homogeneo en banda horaria (6AM/7PM) y en tipo de dia (medianoche).
    // IntervaloTemporal.Segmentar expande cada hora a sus ocurrencias diarias dentro del intervalo,
    // asi que basta declarar las horas del dia una sola vez.
    private static readonly TimeOnly[] FronterasDeSegmentacion =
    [
        new(0, 0),
        FronterasHorariasLegales.InicioDiurna,
        FronterasHorariasLegales.InicioNocturna,
    ];

    // Extra incluye la programada y el excedente.
    private enum Posicion
    {
        Ordinaria,
        Extra,
        Descanso,
    }

    /// <summary>
    /// Clasifica el intervalo trabajado contra la programacion de la franja.
    /// fechaAncla se usa solo para consultar esFestivo al clasificar el tipo de dia de cada segmento.
    /// Precondicion: trabajado es un intervalo valido (no vacio).
    /// </summary>
    public static IReadOnlyList<IntervaloClasificado> Clasificar(
        FranjaProgramada programada,
        IntervaloTemporal trabajado,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
    {
        var inicioFranja = new MomentoDelDia(programada.HoraInicio, 0);
        var finFranja = new MomentoDelDia(programada.HoraFin, programada.DiaOffsetFin);
        var descansos = programada.Descansos.Select(AMinutos).ToList();
        var extras = programada.Extras.Select(AMinutos).ToList();

        // Entrada efectiva = max(trabajado.Inicio, inicioFranja): los minutos previos al inicio
        // programado no se pagan.
        var (trabajadoInicio, trabajadoFin) = Extremos(trabajado);
        var entradaEfectiva = trabajadoInicio.CompareTo(inicioFranja) < 0 ? inicioFranja : trabajadoInicio;
        // Todo el trabajo cae antes del inicio programado (marcaciones de cobertura anticipada): nada que pagar.
        if (trabajadoFin.CompareTo(entradaEfectiva) <= 0) return [];
        var trabajoRecortado = IntervaloTemporal.Crear(entradaEfectiva, trabajadoFin);

        var cortesPrograma = ConstruirCortesPrograma(finFranja, descansos, extras);
        var segmentos = trabajoRecortado
            .Segmentar(FronterasDeSegmentacion)
            .SelectMany(segmento => PartirEn(segmento, cortesPrograma));

        return segmentos
            .Select(segmento => new IntervaloClasificado(
                segmento,
                ClasificarSegmento(segmento, finFranja, descansos, extras, fechaAncla, esFestivo)))
            .ToList();
    }

    private static Concepto ClasificarSegmento(
        IntervaloTemporal segmento,
        MomentoDelDia finFranja,
        IReadOnlyList<(int Inicio, int Fin)> descansos,
        IReadOnlyList<(int Inicio, int Fin)> extras,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
    {
        var (inicio, _) = Extremos(segmento);
        var posicion = ResolverPosicion(inicio.MinutosAbsolutos, finFranja.MinutosAbsolutos, descansos, extras);
        if (posicion == Posicion.Descanso)
            return Concepto.Descanso;

        var banda = ClasificadorHorario.ClasificarBanda(segmento);
        var tipoDia = ClasificadorHorario.ClasificarTipoDia(inicio, fechaAncla, esFestivo);
        return MapearConcepto(posicion, banda, tipoDia);
    }

    // Un segmento esta dentro de una sub-franja si su inicio cae en [Inicio, Fin). Como los cortes de
    // programacion garantizan que ningun segmento atraviesa una frontera de sub-franja, comparar el
    // inicio basta. Excedente = inicio >= finFranja (rango (finFranja, trabajado.Fin]).
    private static Posicion ResolverPosicion(
        int inicioMin,
        int finFranjaMin,
        IReadOnlyList<(int Inicio, int Fin)> descansos,
        IReadOnlyList<(int Inicio, int Fin)> extras)
    {
        if (descansos.Any(d => inicioMin >= d.Inicio && inicioMin < d.Fin))
            return Posicion.Descanso;
        if (inicioMin >= finFranjaMin || extras.Any(e => inicioMin >= e.Inicio && inicioMin < e.Fin))
            return Posicion.Extra;
        return Posicion.Ordinaria;
    }

    // El caso Descanso se resuelve antes de llegar aqui.
    private static Concepto MapearConcepto(Posicion posicion, BandaHoraria banda, TipoDia tipoDia) =>
        (posicion, banda, tipoDia) switch
        {
            (Posicion.Ordinaria, BandaHoraria.Diurna, TipoDia.Habil) => Concepto.OrdinariaDiurna,
            (Posicion.Ordinaria, BandaHoraria.Nocturna, TipoDia.Habil) => Concepto.OrdinariaNocturna,
            (Posicion.Ordinaria, BandaHoraria.Diurna, TipoDia.DominicalFestivo) => Concepto.DominicalFestivaDiurna,
            (Posicion.Ordinaria, BandaHoraria.Nocturna, TipoDia.DominicalFestivo) => Concepto.DominicalFestivaNocturna,
            (Posicion.Extra, BandaHoraria.Diurna, TipoDia.Habil) => Concepto.ExtraDiurna,
            (Posicion.Extra, BandaHoraria.Nocturna, TipoDia.Habil) => Concepto.ExtraNocturna,
            (Posicion.Extra, BandaHoraria.Diurna, TipoDia.DominicalFestivo) => Concepto.ExtraDiurnaDominicalFestiva,
            (Posicion.Extra, BandaHoraria.Nocturna, TipoDia.DominicalFestivo) => Concepto.ExtraNocturnaDominicalFestiva,
            _ => throw new InvalidOperationException($"Combinacion no mapeada: {posicion}/{banda}/{tipoDia}"),
        };

    // El fin de la franja separa la ordinaria del excedente.
    private static IReadOnlyList<int> ConstruirCortesPrograma(
        MomentoDelDia finFranja,
        IReadOnlyList<(int Inicio, int Fin)> descansos,
        IReadOnlyList<(int Inicio, int Fin)> extras)
    {
        var cortes = new List<int> { finFranja.MinutosAbsolutos };
        foreach (var (inicio, fin) in descansos.Concat(extras))
        {
            cortes.Add(inicio);
            cortes.Add(fin);
        }

        return cortes;
    }

    private static IReadOnlyList<IntervaloTemporal> PartirEn(IntervaloTemporal intervalo, IReadOnlyList<int> puntosAbsolutos)
    {
        var (inicio, fin) = Extremos(intervalo);
        var inicioMin = inicio.MinutosAbsolutos;
        var finMin = fin.MinutosAbsolutos;

        var cortes = puntosAbsolutos
            .Where(p => p > inicioMin && p < finMin)
            .Distinct()
            .OrderBy(p => p)
            .ToList();
        if (cortes.Count == 0)
            return [intervalo];

        var resultado = new List<IntervaloTemporal>(cortes.Count + 1);
        var actual = intervalo;
        var baseMin = inicioMin;
        foreach (var corte in cortes)
        {
            var (izquierdo, derecho) = actual.Partir(corte - baseMin);
            resultado.Add(izquierdo);
            actual = derecho;
            baseMin = corte;
        }

        resultado.Add(actual);
        return resultado;
    }

    // [Inicio, Fin) en minutos absolutos.
    private static (int Inicio, int Fin) AMinutos(SubFranjaProgramada sub) =>
        (new MomentoDelDia(sub.HoraInicio, sub.DiaOffsetInicio).MinutosAbsolutos,
         new MomentoDelDia(sub.HoraFin, sub.DiaOffsetFin).MinutosAbsolutos);

    // IntervaloTemporal no expone Inicio/Fin: se reconstruyen anclando a una fecha arbitraria.
    private static (MomentoDelDia Inicio, MomentoDelDia Fin) Extremos(IntervaloTemporal intervalo)
    {
        var (inicio, fin) = intervalo.ResolverA(default);
        return (MomentoDelDia.Desde(inicio, default), MomentoDelDia.Desde(fin, default));
    }
}
