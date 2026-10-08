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
    public bool TieneAdvertencias => throw new NotImplementedException();
}

public static class CalculadorAdvertencias
{
    public static ResultadoAdvertencias Calcular(
        IReadOnlyList<CasillaDia> casillas, JornadaAplicada? jornada, bool esJuzgable) =>
        throw new NotImplementedException();
}
