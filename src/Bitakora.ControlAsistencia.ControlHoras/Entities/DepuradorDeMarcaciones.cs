using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Clase estatica pura que ejecuta el algoritmo secuencial por rango para asignar
// marcaciones normalizadas a franjas ordinarias del turno.
// Sin estado propio - no necesita inyeccion ni ser instanciada.
// Funciones auxiliares (resolver franjas a DateTime, calcular cortes) son internal/private.
// Issue #322: DetalleTurno/DetalleFranjaOrdinaria (PrivateEvents) -> TurnoDiario/FranjaProgramada
// (ControlHoras.DomainEvents, payload por rol).
public static class DepuradorDeMarcaciones
{
    public static IReadOnlyList<ControlFranja> Depurar(
        TurnoDiario? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones)
    {
        // CA-6: sin turno no hay franjas que depurar
        if (turno is null)
            return [];

        var coberturaReferencia = turno.Cobertura(fecha);
        if (coberturaReferencia is null)
            return [];

        DateTime? ancla = marcaciones
            .Select(m => (DateTime?)m.TimestampNormalizado)
            .Where(t => coberturaReferencia.Contiene(t!.Value))
            .Min();
        turno = turno.HorarioEfectivo(fecha, ancla);

        var cobertura = turno.Cobertura(fecha)!;
        marcaciones = marcaciones.Where(m => cobertura.Contiene(m.TimestampNormalizado)).ToList();

        var franjasResueltas = turno.FranjasOrdinarias
            .Select(franja => (Franja: franja, Rango: ResolverRango(franja, fecha)))
            .ToList();

        var rangosDeCaptura = CalcularRangosDeCaptura(franjasResueltas.Select(f => f.Rango).ToList(), cobertura);

        var marcacionesOrdenadas = marcaciones
            .OrderBy(m => m.TimestampNormalizado)
            .ToList();

        return franjasResueltas
            .Select((item, indice) => ConstruirControlFranja(
                item.Franja,
                rangosDeCaptura[indice],
                marcacionesOrdenadas))
            .ToList();
    }

    // Convierte una franja (TimeOnly + DiaOffsetFin) a un rango DateTime relativo a la fecha ancla.
    // Para franjas nocturnas con DiaOffsetFin=1 el fin cae en el dia siguiente.
    private static (DateTime Inicio, DateTime Fin) ResolverRango(FranjaProgramada franja, DateOnly fecha)
    {
        var inicio = fecha.ToDateTime(franja.HoraInicio);
        var fin = fecha.AddDays(franja.DiaOffsetFin).ToDateTime(franja.HoraFin);
        return (inicio, fin);
    }

    // Calcula el rango de captura de cada franja usando puntos medios entre franjas adyacentes.
    // Primera franja empieza en el inicio de la cobertura; ultima termina en el fin de la cobertura.
    private static List<(DateTime Inicio, DateTime Fin)> CalcularRangosDeCaptura(
        IReadOnlyList<(DateTime Inicio, DateTime Fin)> rangosFranja,
        CoberturaDeMarcacion cobertura) =>
        rangosFranja
            .Select((_, indice) => (
                Inicio: indice == 0
                    ? cobertura.Inicio
                    : PuntoMedio(rangosFranja[indice - 1].Fin, rangosFranja[indice].Inicio),
                Fin: indice == rangosFranja.Count - 1
                    ? cobertura.Fin
                    : PuntoMedio(rangosFranja[indice].Fin, rangosFranja[indice + 1].Inicio)))
            .ToList();

    // Punto medio entre dos DateTime usando ticks para evitar perdida de precision.
    private static DateTime PuntoMedio(DateTime a, DateTime b) =>
        a.AddTicks((b - a).Ticks / 2);

    // Construye el ControlFranja asignando primera=Entrada y ultima=Salida dentro del rango.
    // Con cero marcaciones: ambas null. Con una: Entrada poblada, Salida null. Intermedias se descartan.
    private static ControlFranja ConstruirControlFranja(
        FranjaProgramada franja,
        (DateTime Inicio, DateTime Fin) rango,
        IReadOnlyList<MarcacionNormalizada> marcacionesOrdenadas)
    {
        var enRango = marcacionesOrdenadas
            .Where(m => m.TimestampNormalizado >= rango.Inicio && m.TimestampNormalizado < rango.Fin)
            .ToList();

        DateTime? entrada = enRango.Count > 0 ? enRango[0].TimestampNormalizado : null;
        DateTime? salida = enRango.Count > 1 ? enRango[^1].TimestampNormalizado : null;

        return new ControlFranja(franja, entrada, salida);
    }
}
