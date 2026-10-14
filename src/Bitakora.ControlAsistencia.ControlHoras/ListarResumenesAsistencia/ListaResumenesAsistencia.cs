namespace Bitakora.ControlAsistencia.ControlHoras.ListarResumenesAsistencia;

public sealed record ListaResumenesAsistencia(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<ResumenAsistencia> Elementos,
    string? SiguienteCursor);
