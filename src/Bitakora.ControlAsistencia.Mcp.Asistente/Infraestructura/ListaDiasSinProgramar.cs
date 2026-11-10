namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>Contrato upstream de QUERY control-horas/dias-sin-programar, redeclarado aqui (MEF-ADR-0047 decision 3).</summary>
public sealed record ListaDiasSinProgramar(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ElementoDiasSinProgramar> Elementos,
    string? SiguienteCursor);

public sealed record ElementoDiasSinProgramar(string CodigoColaborador, IReadOnlyList<DateOnly> DiasSinProgramar);
