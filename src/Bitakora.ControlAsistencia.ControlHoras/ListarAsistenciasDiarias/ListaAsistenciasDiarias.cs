namespace Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;

public sealed record ListaAsistenciasDiarias(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<FilaAsistenciaDiaria> Elementos,
    string? SiguienteCursor);
