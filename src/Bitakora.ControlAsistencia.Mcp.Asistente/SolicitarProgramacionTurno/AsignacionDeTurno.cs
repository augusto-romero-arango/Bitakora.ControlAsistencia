namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Turno a programar: lo que el ejecutor necesita por turno.</summary>
internal sealed record TurnoAProgramar(Guid Id, string Nombre, bool TieneFranjaSinSede);

/// <summary>Turno que corresponde a cada fecha de la ventana.</summary>
internal sealed class AsignacionDeTurno
{
    private AsignacionDeTurno()
    {
    }

    private Func<DateOnly, TurnoAProgramar> turnoDeLaFecha = null!;

    public bool UsaUnSoloTurno { get; private init; }

    public static AsignacionDeTurno UnSoloTurno(TurnoAProgramar turno) =>
        new() { turnoDeLaFecha = _ => turno, UsaUnSoloTurno = true };

    public static AsignacionDeTurno PorFecha(Func<DateOnly, TurnoAProgramar> turnoDeLaFecha) =>
        new() { turnoDeLaFecha = turnoDeLaFecha };

    public TurnoAProgramar Para(DateOnly fecha) => turnoDeLaFecha(fecha);
}
