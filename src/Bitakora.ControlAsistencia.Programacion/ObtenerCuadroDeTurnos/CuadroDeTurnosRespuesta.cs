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
    private const int MinutosPorHora = 60;

    public static CuadroDeTurnosRespuesta Componer(CuadroDeTurnos cuadro) => new(
        cuadro.Id,
        cuadro.Nombre,
        cuadro.DiasDelCiclo,
        cuadro.Completa,
        cuadro.Dias
            .Select(d => new DiaDelCuadroDeTurnosRespuesta(
                d.Dia,
                new TurnoDelCuadroDeTurnosRespuesta(d.TurnoId, d.Nombre, d.Descripcion, d.Completo, d.Retirado)))
            .ToList(),
        cuadro.JornadaId is { } jornadaId && cuadro.Limites is { } l
            ? new JornadaDelCuadroDeTurnosRespuesta(
                jornadaId,
                AHorasYMinutos(l.HorasSemanalesEnMinutos),
                AHorasYMinutos(l.TopeDiarioEnMinutos),
                AHorasYMinutos(l.MinimoDiarioEnMinutos),
                l.DiasDescansoPorSemana,
                l.Descripcion)
            : null,
        cuadro.Advertencias.Select(ComponerAdvertencia).ToList());

    private static AdvertenciaDelCuadroDeTurnosRespuesta ComponerAdvertencia(AdvertenciaDelCuadroDeTurnos a)
    {
        var magnitud = AHorasYMinutos(a.Magnitud);
        return new AdvertenciaDelCuadroDeTurnosRespuesta(
            a.Tipo, a.Dia, magnitud,
            DescripcionesDeAdvertenciasDeCuadroDeTurnos.Describir(a.Tipo, a.Dia, magnitud));
    }

    private static HorasYMinutosRespuesta AHorasYMinutos(int minutos) =>
        new(minutos / MinutosPorHora, minutos % MinutosPorHora);
}

public sealed record DiaDelCuadroDeTurnosRespuesta(int Dia, TurnoDelCuadroDeTurnosRespuesta Turno);

public sealed record TurnoDelCuadroDeTurnosRespuesta(
    string Id,
    string Nombre,
    string Descripcion,
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
