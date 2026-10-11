namespace Bitakora.ControlAsistencia.ControlHoras.ListarDiasSinProgramar;

public sealed record ElementoDiasSinProgramar(string CodigoColaborador, IReadOnlyList<DateOnly> DiasSinProgramar);

public sealed record ListaDiasSinProgramar(
    DateOnly Desde, DateOnly Hasta, bool RangoRecortado,
    IReadOnlyList<ElementoDiasSinProgramar> Elementos, string? SiguienteCursor);
