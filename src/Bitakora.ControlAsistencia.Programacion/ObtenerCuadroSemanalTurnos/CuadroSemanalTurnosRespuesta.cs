using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.ObtenerCuadroSemanalTurnos;

// DTO de respuesta HTTP: mapeo puro de la vista (sin QuerySession, sin FichaTurno). Los textos de
// presentacion (descripcion de advertencias) se arman aqui desde .resx, no en la vista.
public sealed record CuadroSemanalTurnosRespuesta(
    string Id,
    string Nombre,
    int Semanas,
    bool Completa,
    IReadOnlyList<DiaDelCuadroRespuesta> Dias,
    JornadaDelCuadroRespuesta? Jornada,
    IReadOnlyList<AdvertenciaDelCuadroRespuesta> Advertencias)
{
    private const int MinutosPorHora = 60;

    public static CuadroSemanalTurnosRespuesta Componer(CuadroSemanalTurnos cuadro) => new(
        cuadro.Id,
        cuadro.Nombre,
        cuadro.Semanas,
        cuadro.Completa,
        cuadro.Dias
            .Select(d => new DiaDelCuadroRespuesta(
                d.Semana, d.Dia,
                new TurnoDelCuadroRespuesta(d.TurnoId, d.Nombre, d.Descripcion, d.Completo, d.Retirado)))
            .ToList(),
        cuadro.JornadaId is { } jornadaId && cuadro.Limites is { } l
            ? new JornadaDelCuadroRespuesta(
                jornadaId,
                AHorasYMinutos(l.HorasSemanalesEnMinutos),
                AHorasYMinutos(l.TopeDiarioEnMinutos),
                AHorasYMinutos(l.MinimoDiarioEnMinutos),
                l.DiasDescansoPorSemana,
                l.Descripcion)
            : null,
        cuadro.Advertencias.Select(ComponerAdvertencia).ToList());

    private static AdvertenciaDelCuadroRespuesta ComponerAdvertencia(AdvertenciaDelCuadro a)
    {
        var enDias = a.Tipo is nameof(TipoAdvertenciaPlantilla.FaltanDiasDeDescanso)
            or nameof(TipoAdvertenciaPlantilla.SobranDiasDeDescanso);
        object magnitud = enDias ? a.Magnitud : AHorasYMinutos(a.Magnitud);
        return new AdvertenciaDelCuadroRespuesta(
            a.Tipo, a.Semana, a.Dia, magnitud,
            DescripcionesDeAdvertencias.Describir(a.Tipo, a.Semana, a.Dia, magnitud));
    }

    private static HorasYMinutosRespuesta AHorasYMinutos(int minutos) =>
        new(minutos / MinutosPorHora, minutos % MinutosPorHora);
}

public sealed record DiaDelCuadroRespuesta(int Semana, int Dia, TurnoDelCuadroRespuesta Turno);

public sealed record TurnoDelCuadroRespuesta(
    string Id,
    string? Nombre,
    string? Descripcion,
    bool Completo,
    bool Retirado);

public sealed record JornadaDelCuadroRespuesta(
    Guid Id,
    HorasYMinutosRespuesta HorasSemanales,
    HorasYMinutosRespuesta TopeDiario,
    HorasYMinutosRespuesta MinimoDiario,
    int DiasDescansoPorSemana,
    string Descripcion);

/// <param name="Magnitud">HorasYMinutosRespuesta para las advertencias de horas; int para las de dias.</param>
public sealed record AdvertenciaDelCuadroRespuesta(
    string Tipo,
    int? Semana,
    int? Dia,
    object Magnitud,
    string Descripcion);
