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
    public static CuadroSemanalTurnosRespuesta Componer(CuadroSemanalTurnos cuadro) =>
        throw new NotImplementedException();
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
