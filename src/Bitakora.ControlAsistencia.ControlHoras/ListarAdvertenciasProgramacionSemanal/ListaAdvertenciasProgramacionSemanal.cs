namespace Bitakora.ControlAsistencia.ControlHoras.ListarAdvertenciasProgramacionSemanal;

public sealed record TiempoHM(int Horas, int Minutos)
{
    public static TiempoHM DesdeMinutos(int minutos) => throw new NotImplementedException();
}

public sealed record AdvertenciaPresentada(string Tipo, TiempoHM? Horas, int? Dias, string Descripcion);

public sealed record JornadaPresentada(
    Guid Id, string Descripcion, TiempoHM HorasSemanales, TiempoHM TopeDiario, TiempoHM MinimoDiario,
    int DiasDescansoPorSemana);

public sealed record CasillaPresentada(
    DateOnly Fecha, string Tipo, string NombreTurno, TiempoHM HorasOrdinarias,
    IReadOnlyList<AdvertenciaPresentada> Advertencias, string? MotivoAusencia);

public sealed record ElementoAdvertenciasSemana(
    string CodigoColaborador, string NombreCompleto, JornadaPresentada? Jornada,
    TiempoHM HorasOrdinariasProgramadas, bool EsJuzgable, string? MotivoNoJuzgable, int DiasSinProgramar,
    bool TieneAusencias, IReadOnlyList<AdvertenciaPresentada> Advertencias,
    IReadOnlyList<CasillaPresentada> Casillas);

public sealed record ListaAdvertenciasProgramacionSemanal(
    DateOnly Desde, DateOnly Hasta, int AnioIso, int NumeroSemana,
    IReadOnlyList<ElementoAdvertenciasSemana> Elementos, string? SiguienteCursor);
