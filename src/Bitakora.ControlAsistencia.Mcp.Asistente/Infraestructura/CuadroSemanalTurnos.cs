namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record CuadroSemanalTurnos(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<DiaDelCuadro> Dias);

public sealed record DiaDelCuadro(int Semana, int Dia, TurnoDelCuadro Turno);

public sealed record TurnoDelCuadro(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);
