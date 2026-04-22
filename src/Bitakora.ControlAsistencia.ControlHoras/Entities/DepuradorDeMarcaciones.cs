using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-122: Clase estatica pura que ejecuta el algoritmo secuencial por rango para asignar
// marcaciones normalizadas a franjas ordinarias del turno.
// Sin estado propio - no necesita inyeccion ni ser instanciada.
// Funciones auxiliares (resolver franjas a DateTime, calcular cortes) son internal/private.
public static class DepuradorDeMarcaciones
{
    public static IReadOnlyList<ControlFranja> Depurar(
        DetalleTurno? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones)
    {
        // CA-6: sin turno no hay franjas que depurar
        if (turno is null)
            return [];

        var franjasResueltas = turno.FranjasOrdinarias
            .Select(franja => (Franja: franja, Rango: ResolverRango(franja, fecha)))
            .ToList();

        var rangosDeCaptura = CalcularRangosDeCaptura(franjasResueltas.Select(f => f.Rango).ToList());

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
    private static (DateTime Inicio, DateTime Fin) ResolverRango(DetalleFranjaOrdinaria franja, DateOnly fecha)
    {
        var inicio = fecha.ToDateTime(franja.HoraInicio);
        var fin = fecha.AddDays(franja.DiaOffsetFin).ToDateTime(franja.HoraFin);
        return (inicio, fin);
    }

    // Calcula el rango de captura de cada franja usando puntos medios entre franjas adyacentes.
    // Primera franja empieza en DateTime.MinValue; ultima termina en DateTime.MaxValue.
    private static List<(DateTime Inicio, DateTime Fin)> CalcularRangosDeCaptura(
        IReadOnlyList<(DateTime Inicio, DateTime Fin)> rangosFranja) =>
        rangosFranja
            .Select((_, indice) => (
                Inicio: indice == 0
                    ? DateTime.MinValue
                    : PuntoMedio(rangosFranja[indice - 1].Fin, rangosFranja[indice].Inicio),
                Fin: indice == rangosFranja.Count - 1
                    ? DateTime.MaxValue
                    : PuntoMedio(rangosFranja[indice].Fin, rangosFranja[indice + 1].Inicio)))
            .ToList();

    // Punto medio entre dos DateTime usando ticks para evitar perdida de precision.
    private static DateTime PuntoMedio(DateTime a, DateTime b) =>
        a.AddTicks((b - a).Ticks / 2);

    // Construye el ControlFranja asignando primera=Entrada y ultima=Salida dentro del rango.
    // Con cero marcaciones: ambas null. Con una: Entrada poblada, Salida null. Intermedias se descartan.
    private static ControlFranja ConstruirControlFranja(
        DetalleFranjaOrdinaria franja,
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
