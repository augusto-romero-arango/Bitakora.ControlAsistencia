namespace Bitakora.ControlAsistencia.ReadModels.Programacion;

/// <summary>
/// Cuadro de turnos de una plantilla de turnos: dias del ciclo con su turno y la Jornada asociada.
/// </summary>
/// <param name="Completa">Estado derivado: DiasDelCiclo dias con turno no retirado y completo.</param>
/// <param name="JornadaId">Jornada asociada a la plantilla, o null sin Jornada.</param>
[MaterializadoPorProyeccion]
public sealed record CuadroDeTurnos(
    string Id,
    string Nombre,
    int DiasDelCiclo,
    IReadOnlyList<DiaDelCuadroDeTurnos> Dias,
    bool Completa,
    Guid? JornadaId);

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
