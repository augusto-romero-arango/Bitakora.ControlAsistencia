using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;

public sealed record CuadroDeTurnosRespuesta(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDelCuadroDeTurnosRespuesta> Dias,
    JornadaDelCuadroDeTurnosRespuesta? Jornada,
    IReadOnlyList<AdvertenciaDelCuadroDeTurnosRespuesta> Advertencias)
{
    public static CuadroDeTurnosRespuesta Componer(CuadroDeTurnos cuadro) =>
        throw new NotImplementedException();
}

public sealed record DiaDelCuadroDeTurnosRespuesta(int Dia, TurnoDelCuadroDeTurnosRespuesta Turno);

public sealed record TurnoDelCuadroDeTurnosRespuesta(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);

public sealed record JornadaDelCuadroDeTurnosRespuesta(
    Guid Id,
    HorasYMinutosRespuesta HorasSemanales,
    HorasYMinutosRespuesta TopeDiario,
    HorasYMinutosRespuesta MinimoDiario,
    int DiasDescansoPorSemana,
    string Descripcion);

public sealed record AdvertenciaDelCuadroDeTurnosRespuesta(
    string Tipo,
    int? Dia,
    HorasYMinutosRespuesta Magnitud,
    string Descripcion);
