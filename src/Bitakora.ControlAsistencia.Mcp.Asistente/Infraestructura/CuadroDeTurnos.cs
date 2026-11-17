namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record CuadroDeTurnos(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDelCuadroDeTurnos> Dias,
    string? JornadaId = null);

public sealed record DiaDelCuadroDeTurnos(int Dia, TurnoDelCuadroDeTurnos Turno);

public sealed record TurnoDelCuadroDeTurnos(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado,
    bool EsDescanso = false,
    HorarioAbiertoDelCuadroDeTurnos? HorarioAbierto = null);

public sealed record HorarioAbiertoDelCuadroDeTurnos(string Duracion);
