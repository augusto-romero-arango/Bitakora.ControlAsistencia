using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;

/// <summary>Turno a programar: lo que el ejecutor necesita por turno.</summary>
internal sealed record TurnoAProgramar(Guid Id, string Nombre, bool TieneFranjaSinSede)
{
    public static TurnoAProgramar De(FichaTurno ficha) =>
        new(Guid.Parse(ficha.Id), ficha.Nombre, ficha.Franjas.Any(f => f.SedeId is null));
}

/// <summary>Turno que corresponde a cada fecha de la ventana.</summary>
internal sealed class AsignacionDeTurno
{
    private readonly Func<DateOnly, TurnoAProgramar> turnoDeLaFecha;

    private AsignacionDeTurno(Func<DateOnly, TurnoAProgramar> turnoDeLaFecha, bool usaUnSoloTurno)
    {
        this.turnoDeLaFecha = turnoDeLaFecha;
        UsaUnSoloTurno = usaUnSoloTurno;
    }

    public bool UsaUnSoloTurno { get; }

    public static AsignacionDeTurno UnSoloTurno(TurnoAProgramar turno) => new(_ => turno, usaUnSoloTurno: true);

    public static AsignacionDeTurno PorFecha(Func<DateOnly, TurnoAProgramar> turnoDeLaFecha) =>
        new(turnoDeLaFecha, usaUnSoloTurno: false);

    public TurnoAProgramar Para(DateOnly fecha) => turnoDeLaFecha(fecha);
}
