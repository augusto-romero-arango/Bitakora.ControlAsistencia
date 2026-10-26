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
    PorDebajoDelMinimoDiario,
    PorDebajoDelDescansoEntreTurnos,
    SeSolapaConTurnoDeLaVispera
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
        var diarias = casillas.Select(c => AdvertenciasDelDia(c, jornada)).ToList();
        if (jornada is null)
            return new ResultadoAdvertencias([], diarias);

        var semanales = new List<AdvertenciaSemanal>();
        var excesoHoras = casillas.Sum(c => c.MinutosOrdinarios) - jornada.HorasSemanalesEnMinutos;
        if (excesoHoras > 0)
            semanales.Add(new(TipoAdvertenciaSemanal.SuperaHorasSemanales, excesoHoras));
        else if (esJuzgable && excesoHoras < 0)
            semanales.Add(new(TipoAdvertenciaSemanal.PorDebajoDeHorasSemanales, -excesoHoras));

        if (esJuzgable && jornada.DiasDescansoPorSemana > 0)
        {
            var excesoDescansos = casillas.Count(c => c.Tipo == TipoCasilla.Descanso) - jornada.DiasDescansoPorSemana;
            if (excesoDescansos < 0)
                semanales.Add(new(TipoAdvertenciaSemanal.FaltanDiasDeDescanso, -excesoDescansos));
            else if (excesoDescansos > 0)
                semanales.Add(new(TipoAdvertenciaSemanal.SobranDiasDeDescanso, excesoDescansos));
        }

        return new ResultadoAdvertencias(semanales, diarias);
    }

    private static IReadOnlyList<AdvertenciaDiaria> AdvertenciasDelDia(CasillaDia casilla, JornadaAplicada? jornada) =>
        [.. ContraLaJornada(casilla, jornada), .. EntreTurnos(casilla)];

    // 2 h + 4 h de margenes de la Cobertura de marcacion (CA-ADR-0042). Si cambian, cambia esta constante.
    private static readonly TimeSpan DescansoMinimoEntreTurnos = TimeSpan.FromHours(6);

    // El solape reemplaza al descanso entre turnos. Fin exclusivo: turnos contiguos no se solapan.
    // Con ventana se juzga contra el cierre de la ventana (entrada mas tardia posible).
    private static IEnumerable<AdvertenciaDiaria> EntreTurnos(CasillaDia casilla)
    {
        if (casilla.Tipo != TipoCasilla.Trabajo)
            yield break;

        if (casilla is { CierreVentanaEntrada: { } cierreVentana, CierreTurnoVispera: { } cierre })
        {
            if (cierre > cierreVentana)
            {
                yield return new(TipoAdvertenciaDiaria.SeSolapaConTurnoDeLaVispera, (int)(cierre - cierreVentana).TotalMinutes);
                yield break;
            }

            if (casilla.TieneVentanaDeEntrada)
            {
                var entradaMinima = cierre + DescansoMinimoEntreTurnos;
                if (entradaMinima > cierreVentana)
                    yield return new(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, (int)(entradaMinima - cierreVentana).TotalMinutes);
                yield break;
            }
        }

        if (!casilla.TieneVentanaDeEntrada
            && casilla is { InicioCobertura: { } inicio, LimiteCoberturaVispera: { } limite }
            && limite > inicio)
            yield return new(TipoAdvertenciaDiaria.PorDebajoDelDescansoEntreTurnos, (int)(limite - inicio).TotalMinutes);
    }

    private static IReadOnlyList<AdvertenciaDiaria> ContraLaJornada(CasillaDia casilla, JornadaAplicada? jornada)
    {
        if (jornada is null || casilla.Tipo != TipoCasilla.Trabajo)
            return [];
        if (casilla.MinutosOrdinarios > jornada.TopeDiarioEnMinutos)
            return [new(TipoAdvertenciaDiaria.SuperaTopeDiario, casilla.MinutosOrdinarios - jornada.TopeDiarioEnMinutos)];
        if (casilla.MinutosOrdinarios < jornada.MinimoDiarioEnMinutos)
            return [new(TipoAdvertenciaDiaria.PorDebajoDelMinimoDiario, jornada.MinimoDiarioEnMinutos - casilla.MinutosOrdinarios)];
        return [];
    }
}
