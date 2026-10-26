using System.Collections.ObjectModel;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

public static class DepuradorDeMarcaciones
{
    public static IReadOnlyList<ControlFranja> Depurar(
        TurnoDiario? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones,
        CoberturasVecinas? vecinas = null)
    {
        if (Resolver(turno, fecha, marcaciones, vecinas) is not (var horarioEfectivo, _, var marcacionesOrdenadas))
            return [];

        var franjasResueltas = horarioEfectivo.FranjasOrdinarias
            .Select(franja => (Franja: franja, Rango: ResolverRango(franja, fecha)))
            .ToList();

        var rangosDeCaptura = CalcularRangosDeCaptura(franjasResueltas.Select(f => f.Rango).ToList());

        return franjasResueltas
            .Select((item, indice) => ConstruirControlFranja(
                item.Franja,
                rangosDeCaptura[indice],
                marcacionesOrdenadas))
            .ToList();
    }

    public static IReadOnlyDictionary<DateTime, PertenenciaMarcacion> Clasificar(
        TurnoDiario? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones,
        CoberturasVecinas? vecinas = null)
    {
        if (Resolver(turno, fecha, marcaciones, vecinas) is not (_, var cobertura, _))
            return ReadOnlyDictionary<DateTime, PertenenciaMarcacion>.Empty;

        return marcaciones
            .Select(m => m.TimestampNormalizado)
            .Distinct()
            .ToDictionary(
                instante => instante,
                instante => (cobertura.Contiene(instante) && vecinas?.Cede(instante) != true,
                    vecinas?.Excluye(instante) == true) switch
                {
                    (false, _) => PertenenciaMarcacion.FueraDeCobertura,
                    (true, true) => PertenenciaMarcacion.Ambigua,
                    _ => PertenenciaMarcacion.DelTurno
                });
    }

    public static CoberturaDeMarcacion? CoberturaEfectiva(
        TurnoDiario? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones,
        CoberturasVecinas? vecinas = null) =>
        Resolver(turno, fecha, marcaciones, vecinas)?.Cobertura;

    // Las marcaciones en la cobertura de un vecino no se usan ni como ancla ni como entrada o salida.
    private static (TurnoDiario Horario, CoberturaDeMarcacion Cobertura, List<MarcacionNormalizada> MarcacionesOrdenadas)? Resolver(
        TurnoDiario? turno,
        DateOnly fecha,
        IReadOnlyList<MarcacionNormalizada> marcaciones,
        CoberturasVecinas? vecinas)
    {
        if (turno?.CoberturaPosible(fecha) is not { } coberturaPosible)
            return null;

        var usables = marcaciones
            .Where(m => vecinas?.Excluye(m.TimestampNormalizado) != true)
            .ToList();
        var ancla = usables
            .Where(m => coberturaPosible.Contiene(m.TimestampNormalizado))
            .Min(m => (DateTime?)m.TimestampNormalizado);
        var horarioEfectivo = turno.HorarioEfectivo(fecha, ancla);

        // HorarioEfectivo conserva las franjas, asi que la cobertura efectiva existe.
        var cobertura = horarioEfectivo.Cobertura(fecha)!;
        var marcacionesOrdenadas = usables
            .Where(m => cobertura.Contiene(m.TimestampNormalizado))
            .OrderBy(m => m.TimestampNormalizado)
            .ToList();
        return (horarioEfectivo, cobertura, marcacionesOrdenadas);
    }

    private static (DateTime Inicio, DateTime Fin) ResolverRango(FranjaProgramada franja, DateOnly fecha)
    {
        var inicio = fecha.ToDateTime(franja.HoraInicio);
        var fin = fecha.AddDays(franja.DiaOffsetFin).ToDateTime(franja.HoraFin);
        return (inicio, fin);
    }

    // Entre franjas adyacentes el corte es el punto medio. Los extremos quedan abiertos porque las
    // marcaciones ya llegan acotadas a la cobertura del horario efectivo.
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

    private static DateTime PuntoMedio(DateTime a, DateTime b) =>
        a.AddTicks((b - a).Ticks / 2);

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
