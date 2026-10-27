namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed record CuadroDeTurnos(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDelCuadroDeTurnos> Dias,
    JornadaDelCuadroDeTurnos? Jornada = null,
    IReadOnlyList<AdvertenciaDelCuadroDeTurnos>? Advertencias = null);

public sealed record DiaDelCuadroDeTurnos(int Dia, TurnoDelCuadroDeTurnos Turno);

public sealed record TurnoDelCuadroDeTurnos(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);

public sealed record JornadaDelCuadroDeTurnos(string Id, string Descripcion)
{
    public JornadaDescritaResumen Resumir() => new(Id, Descripcion);
}

public sealed record AdvertenciaDelCuadroDeTurnos(string Descripcion);
