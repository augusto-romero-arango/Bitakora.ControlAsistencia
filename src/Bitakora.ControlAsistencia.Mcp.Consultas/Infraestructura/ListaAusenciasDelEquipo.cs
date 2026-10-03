namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY programacion/ausencias, redeclarado aqui (cero referencias a los
/// ensamblados del BC).
/// </summary>
public sealed record ListaAusenciasDelEquipo(
    DateOnly Desde,
    DateOnly Hasta,
    bool RangoRecortado,
    IReadOnlyList<AusenciasDeColaborador> Colaboradores);

public sealed record AusenciasDeColaborador(
    string CodigoColaborador,
    string NombreCompleto,
    IReadOnlyList<AusenciaDelPeriodo> Ausencias);

public sealed record AusenciaDelPeriodo(Guid Id, string Motivo, IReadOnlyList<TramoAplicado> Tramos);

public sealed record TramoAplicado(DateOnly Desde, DateOnly Hasta);
