namespace Bitakora.ControlAsistencia.ReadModels.ControlHoras;

public enum TipoAdvertenciaSemanal
{
    SuperaHorasSemanales,
    PorDebajoDeHorasSemanales,
    FaltanDiasDeDescanso,
    SobranDiasDeDescanso
}

public enum TipoAdvertenciaDiaria
{
    SuperaTopeDiario,
    PorDebajoDelMinimoDiario
}

/// <summary>Magnitud en minutos para las horas semanales y en dias para los descansos.</summary>
public sealed record AdvertenciaSemanal(TipoAdvertenciaSemanal Tipo, int Magnitud);

public sealed record AdvertenciaDiaria(TipoAdvertenciaDiaria Tipo, int MagnitudEnMinutos);

/// <summary>Diarias es paralela a las casillas recibidas (una lista por casilla).</summary>
public sealed record ResultadoAdvertencias(
    IReadOnlyList<AdvertenciaSemanal> Semanales,
    IReadOnlyList<IReadOnlyList<AdvertenciaDiaria>> Diarias)
{
    public bool TieneAdvertencias => Semanales.Count > 0 || Diarias.Any(d => d.Count > 0);
}

public static class CalculadorAdvertencias
{
    public static ResultadoAdvertencias Calcular(
        IReadOnlyList<CasillaDia> casillas, JornadaAplicada? jornada, bool esJuzgable)
    {
        if (jornada is null)
            return new ResultadoAdvertencias(
                [],
                casillas.Select(_ => (IReadOnlyList<AdvertenciaDiaria>)[]).ToList());

        var diarias = casillas.Select(c => Diarias(c, jornada)).ToList();

        var semanales = new List<AdvertenciaSemanal>();
        var total = casillas.Sum(c => c.MinutosOrdinarios);
        if (total > jornada.HorasSemanalesEnMinutos)
            semanales.Add(new(TipoAdvertenciaSemanal.SuperaHorasSemanales, total - jornada.HorasSemanalesEnMinutos));
        else if (esJuzgable && total < jornada.HorasSemanalesEnMinutos)
            semanales.Add(new(TipoAdvertenciaSemanal.PorDebajoDeHorasSemanales, jornada.HorasSemanalesEnMinutos - total));

        if (esJuzgable && jornada.DiasDescansoPorSemana > 0)
        {
            var descansos = casillas.Count(c => c.Tipo == TipoCasilla.Descanso);
            if (descansos < jornada.DiasDescansoPorSemana)
                semanales.Add(new(TipoAdvertenciaSemanal.FaltanDiasDeDescanso, jornada.DiasDescansoPorSemana - descansos));
            else if (descansos > jornada.DiasDescansoPorSemana)
                semanales.Add(new(TipoAdvertenciaSemanal.SobranDiasDeDescanso, descansos - jornada.DiasDescansoPorSemana));
        }

        return new ResultadoAdvertencias(semanales, diarias);
    }

    private static IReadOnlyList<AdvertenciaDiaria> Diarias(CasillaDia casilla, JornadaAplicada jornada)
    {
        if (casilla.Tipo != TipoCasilla.Trabajo)
            return [];
        if (casilla.MinutosOrdinarios > jornada.TopeDiarioEnMinutos)
            return [new(TipoAdvertenciaDiaria.SuperaTopeDiario, casilla.MinutosOrdinarios - jornada.TopeDiarioEnMinutos)];
        if (casilla.MinutosOrdinarios < jornada.MinimoDiarioEnMinutos)
            return [new(TipoAdvertenciaDiaria.PorDebajoDelMinimoDiario, jornada.MinimoDiarioEnMinutos - casilla.MinutosOrdinarios)];
        return [];
    }
}
