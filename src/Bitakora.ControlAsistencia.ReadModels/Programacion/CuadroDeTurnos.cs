namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Cuadro de turnos de una plantilla de turnos: dias del ciclo con su turno, la Jornada contra la que
/// se audita y las advertencias vigentes.
/// </summary>
/// <param name="Completa">Estado derivado: DiasDelCiclo dias con turno no retirado y completo.</param>
/// <param name="Limites">Copia de los limites de la Jornada, o null sin Jornada.</param>
[MaterializadoPorProyeccion]
public sealed record CuadroDeTurnos(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    IReadOnlyList<DiaDelCuadroDeTurnos> Dias,
    bool Completa,
    Guid? JornadaId,
    LimitesDelCuadro? Limites,
    IReadOnlyList<AdvertenciaDelCuadroDeTurnos> Advertencias);

/// <summary>Un dia con turno del cuadro; Dia es su numero en el ciclo.</summary>
public sealed record DiaDelCuadroDeTurnos(
    int Dia,
    string TurnoId,
    string Nombre,
    string Descripcion,
    bool Completo,
    bool Retirado,
    bool EsDescanso = false,
    HorarioAbiertoDelCuadro? HorarioAbierto = null);

/// <summary>Duracion legible de un turno de horario abierto; null en el dia = no es de horario abierto.</summary>
public sealed record HorarioAbiertoDelCuadro(string Duracion);

/// <summary>Advertencia estructurada: Tipo es el nombre del tipo; Magnitud en minutos o dias.</summary>
public sealed record AdvertenciaDelCuadroDeTurnos(string Tipo, int? Dia, int Magnitud);

/// <summary>Limites de la Jornada en minutos y dias, con su descripcion calculada al proyectar.</summary>
public sealed record LimitesDelCuadro(
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana,
    string Descripcion);
