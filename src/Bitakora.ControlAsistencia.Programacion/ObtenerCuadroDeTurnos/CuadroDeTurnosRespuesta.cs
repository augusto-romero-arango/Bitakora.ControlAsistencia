using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroDeTurnos;

public sealed record CuadroDeTurnosRespuesta(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    bool Completa,
    IReadOnlyList<DiaDelCuadroDeTurnosRespuesta> Dias,
    Guid? JornadaId)
{
    public static CuadroDeTurnosRespuesta Componer(CuadroDeTurnos cuadro) => new(
        cuadro.Id,
        cuadro.Nombre,
        cuadro.DiasDelCiclo,
        cuadro.Completa,
        cuadro.Dias
            .Select(d => new DiaDelCuadroDeTurnosRespuesta(
                d.Dia,
                new TurnoDelCuadroDeTurnosRespuesta(d.TurnoId, d.Nombre, d.Descripcion, d.Completo, d.Retirado,
                    d.EsDescanso,
                    d.HorarioAbierto is { } h ? new HorarioAbiertoDelCuadroDeTurnosRespuesta(h.Duracion) : null)))
            .ToList(),
        cuadro.JornadaId);
}

public sealed record DiaDelCuadroDeTurnosRespuesta(int Dia, TurnoDelCuadroDeTurnosRespuesta Turno);

public sealed record TurnoDelCuadroDeTurnosRespuesta(
    string Id,
    string Nombre,
    string Descripcion,
    bool Completo,
    bool Retirado,
    bool EsDescanso = false,
    HorarioAbiertoDelCuadroDeTurnosRespuesta? HorarioAbierto = null);

public sealed record HorarioAbiertoDelCuadroDeTurnosRespuesta(string Duracion);
