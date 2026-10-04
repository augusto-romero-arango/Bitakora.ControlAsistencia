namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Turno a programar: lo que el ejecutor necesita por turno.</summary>
internal sealed record TurnoAProgramar(Guid Id, string Nombre, bool TieneFranjaSinSede);

/// <summary>Turno que corresponde a cada fecha de la ventana.</summary>
internal sealed class AsignacionDeTurno
{
    private AsignacionDeTurno()
    {
    }

    public bool UsaUnSoloTurno => throw new NotImplementedException();

    public static AsignacionDeTurno UnSoloTurno(TurnoAProgramar turno) => throw new NotImplementedException();

    public static AsignacionDeTurno PorFecha(Func<DateOnly, TurnoAProgramar> turnoDeLaFecha) =>
        throw new NotImplementedException();

    public TurnoAProgramar Para(DateOnly fecha) => throw new NotImplementedException();
}
