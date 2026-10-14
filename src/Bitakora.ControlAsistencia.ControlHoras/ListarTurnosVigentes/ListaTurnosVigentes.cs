namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

public sealed record ListaTurnosVigentes(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<TurnoVigenteRespuesta> Elementos,
    string? SiguienteCursor);
