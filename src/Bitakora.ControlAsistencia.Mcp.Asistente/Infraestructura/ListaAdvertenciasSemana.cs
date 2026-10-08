namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

/// <summary>
/// Contrato upstream de QUERY control-horas/advertencias-programacion-semanal, redeclarado como
/// isla (MEF-ADR-0047 decision 3): solo los campos que la tool consume.
/// </summary>
public sealed record ListaAdvertenciasSemana(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<ElementoAdvertenciasSemana> Elementos,
    string? SiguienteCursor);

public sealed record ElementoAdvertenciasSemana(
    string CodigoColaborador,
    string NombreCompleto,
    IReadOnlyList<AdvertenciaSemana> Advertencias,
    IReadOnlyList<CasillaAdvertenciasSemana> Casillas);

public sealed record CasillaAdvertenciasSemana(DateOnly Fecha, IReadOnlyList<AdvertenciaSemana> Advertencias);

public sealed record AdvertenciaSemana(string Tipo, string Descripcion);
