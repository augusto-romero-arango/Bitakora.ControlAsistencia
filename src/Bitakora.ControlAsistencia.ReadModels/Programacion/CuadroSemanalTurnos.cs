namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Cuadro semanal de turnos de una plantilla: grilla dias x turnos con la Jornada contra la que se
/// audita y las advertencias vigentes, para que el Programador la corrija antes de aplicarla.
/// </summary>
/// <remarks>
/// Record plano SIN partial ni comportamiento (MEF-ADR-0035). Vive en ReadModels, isla sin
/// ProjectReference (CA-ADR-0029): todo es tipo propio, nunca uno de Programacion.DomainEvents.
/// Id es el stream key de la plantilla. Todo llega en los eventos de la propia plantilla
/// (CA-ADR-0034 enmendado por #886): sin composicion con FichaTurno. Sin texto de presentacion.
/// </remarks>
/// <param name="Completa">Estado derivado: 7 x Semanas dias con turno no retirado y completo.</param>
/// <param name="Limites">Copia de los limites de la Jornada, o null sin Jornada.</param>
/// <param name="Advertencias">Ultimo AdvertenciasDePlantillaSemanalCalculadas; vacia sin evento.</param>
public sealed record CuadroSemanalTurnos(
    string Id,
    string Nombre,
    int Semanas,
    IReadOnlyList<DiaDelCuadro> Dias,
    bool Completa,
    Guid? JornadaId,
    LimitesDelCuadro? Limites,
    IReadOnlyList<AdvertenciaDelCuadro> Advertencias);

/// <summary>Un dia asignado del cuadro, con la copia plana de su turno. Dia es el numero ISO (1..7).</summary>
public sealed record DiaDelCuadro(
    int Semana,
    int Dia,
    string TurnoId,
    string Nombre,
    string Descripcion,
    bool Completo,
    bool Retirado);

/// <summary>Limites de la Jornada en minutos y dias, con su descripcion calculada al proyectar.</summary>
public sealed record LimitesDelCuadro(
    int HorasSemanalesEnMinutos,
    int TopeDiarioEnMinutos,
    int MinimoDiarioEnMinutos,
    int DiasDescansoPorSemana,
    string Descripcion);

/// <summary>Advertencia estructurada: Tipo es el codigo (nombre del tipo); Magnitud en minutos o dias.</summary>
public sealed record AdvertenciaDelCuadro(string Tipo, int? Semana, int? Dia, int Magnitud);
