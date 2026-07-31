// Issue #135: Clasificar intervalos trabajados segun programacion de la franja.
// Capa 3 (Parte C) del calculo de desglose de horas: integra la segmentacion (#115) y
// la clasificacion banda+tipo (#134) con la programacion del turno (DetalleFranjaOrdinaria):
// descansos, extras programadas y excedente (salida mas alla del fin programado).
// Trabaja siempre en MomentoDelDia - no construye ningun DateTime en este nivel.
using Bitakora.ControlAsistencia.ControlHoras.ValueObjects;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

/// <summary>
/// Produce la lista de IntervaloClasificado que describe cada minuto trabajado con su concepto legal,
/// dado el contexto de una franja programada. La entrada efectiva es max(trabajado.Inicio, inicioFranja):
/// los minutos antes del inicio programado no aparecen en el desglose.
/// </summary>
public static class ClasificadorTrabajo
{
    // Fronteras de segmentacion previas a la programacion. Cada segmento producido es homogeneo en
    // banda horaria (cortes en 6AM/7PM) y en tipo de dia (corte en medianoche, donde puede cambiar
    // de habil a dominical/festivo). IntervaloTemporal.Segmentar expande cada hora a sus ocurrencias
    // diarias dentro del intervalo, asi que basta con declarar las horas-del-dia una sola vez.
    private static readonly TimeOnly[] FronterasDeSegmentacion =
    [
        new(0, 0), // medianoche: separa segmentos con distinto tipo de dia
        FronterasHorariasLegales.InicioDiurna,
        FronterasHorariasLegales.InicioNocturna,
    ];

    // Posicion del segmento respecto a la programacion. Determina si el concepto es ordinario,
    // extra (programada o excedente) o descanso. Independiente de banda y tipo de dia.
    private enum Posicion
    {
        Ordinaria,
        Extra,
        Descanso,
    }

    /// <summary>
    /// Clasifica el intervalo trabajado contra la programacion de la franja.
    /// fechaAncla se usa solo para consultar esFestivo al clasificar el tipo de dia de cada segmento.
    /// Precondicion: trabajado es un intervalo valido (no vacio); la franja anomala se maneja en #136.
    /// </summary>
    public static IReadOnlyList<IntervaloClasificado> Clasificar(
        DetalleFranjaOrdinaria programada,
        IntervaloTemporal trabajado,
        DateOnly fechaAncla,
        Func<DateOnly, bool> esFestivo)
    {
        // 1. Resolver las fronteras programadas a MomentoDelDia / minutos absolutos.
        var inicioFranja = new MomentoDelDia(programada.HoraInicio, 0);
        var finFranja = new MomentoDelDia(programada.HoraFin, programada.DiaOffsetFin);
        var descansos = programada.Descansos.Select(AMinutos).ToList();
        var extras = programada.Extras.Select(AMinutos).ToList();

        // 2. Entrada efectiva = max(trabajado.Inicio, inicioFranja): recorta los minutos previos al
        // inicio programado, que no se pagan y no aparecen en el desglose.
        var (trabajadoInicio, trabajadoFin) = Extremos(trabajado);
        var entradaEfectiva = trabajadoInicio.CompareTo(inicioFranja) < 0 ? inicioFranja : trabajadoInicio;
        var trabajoRecortado = IntervaloTemporal.Crear(entradaEfectiva, trabajadoFin);

        // 3. Segmentar por banda horaria y por medianoche (cambio de tipo de dia).
        // 4. Subdividir cada segmento en los cortes propios de la programacion (fin de franja,
        // inicios/fines de descansos y extras).
        var cortesPrograma = ConstruirCortesPrograma(finFranja, descansos, extras);
        var segmentos = trabajoRecortado
            .Segmentar(FronterasDeSegmentacion)
            .SelectMany(segmento => PartirEn(segmento, cortesPrograma));

        // 5-6. Clasificar cada segmento (banda + tipo de dia + posicion) y mapear a Concepto.
        return segmentos
            .Select(segmento => new IntervaloClasificado(
                segmento,
                ClasificarSegmento(segmento, finFranja, descansos, extras, fechaAncla, esFestivo)))
            .ToList();
    }

    // Concepto de un segmento homogeneo. El descanso ignora banda y tipo de dia; el resto combina
    // posicion (ordinaria/extra), banda (diurna/nocturna) y tipo de dia (habil/dominical-festivo).
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

    // Mapeo (posicion, banda, tipoDia) -> Concepto segun la tabla del issue #135.
    // El caso Descanso se resuelve antes de llegar aqui; quedan las 8 combinaciones ordinaria/extra.
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

    // Cortes que la programacion impone sobre los segmentos de banda: el fin de la franja (separa
    // ordinaria de excedente) y las fronteras de cada descanso y extra.
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

    // Parte un intervalo en cada punto (en minutos absolutos) estrictamente interior a el.
    // Reutiliza IntervaloTemporal.Partir (#143); los puntos fuera del intervalo se ignoran.
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

    // Convierte una sub-franja (descanso o extra) a su par de minutos absolutos [Inicio, Fin).
    private static (int Inicio, int Fin) AMinutos(DetalleSubFranja sub) =>
        (new MomentoDelDia(sub.HoraInicio, sub.DiaOffsetInicio).MinutosAbsolutos,
         new MomentoDelDia(sub.HoraFin, sub.DiaOffsetFin).MinutosAbsolutos);

    // Recupera los extremos de un IntervaloTemporal como MomentoDelDia. El intervalo encapsula sus
    // extremos (PR #148, Tell-don't-Ask): no expone Inicio/Fin. Se reconstruyen via el par
    // ResolverA/MomentoDelDia.Desde anclado a una fecha arbitraria (no se construye ningun DateTime
    // de dominio), igual que hace ClasificadorHorario para leer la hora del inicio.
    private static (MomentoDelDia Inicio, MomentoDelDia Fin) Extremos(IntervaloTemporal intervalo)
    {
        var (inicio, fin) = intervalo.ResolverA(default);
        return (MomentoDelDia.Desde(inicio, default), MomentoDelDia.Desde(fin, default));
    }
}
