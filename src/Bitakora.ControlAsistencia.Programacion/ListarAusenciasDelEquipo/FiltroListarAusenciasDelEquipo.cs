namespace Bitakora.ControlAsistencia.Programacion.ListarAusenciasDelEquipo;

public sealed record FiltroListarAusenciasDelEquipo(
    DateOnly? Desde,
    DateOnly? Hasta,
    IReadOnlyList<string>? Colaboradores);
