using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;

/// <summary>Turno del ciclo que corresponde a cada fecha, con el dia 1 cayendo en la fecha de inicio.</summary>
internal sealed class AsignacionDeCiclo
{
    private readonly int diasDelCiclo;
    private readonly DateOnly fechaInicio;
    private readonly IReadOnlyDictionary<int, TurnoDelCuadroDeTurnos> turnos;

    private AsignacionDeCiclo(
        int diasDelCiclo, DateOnly fechaInicio, IReadOnlyDictionary<int, TurnoDelCuadroDeTurnos> turnos)
    {
        this.diasDelCiclo = diasDelCiclo;
        this.fechaInicio = fechaInicio;
        this.turnos = turnos;
    }

    public static AsignacionDeCiclo Crear(CuadroDeTurnos cuadro, DateOnly fechaInicio) =>
        new(cuadro.DiasDelCiclo, fechaInicio, cuadro.Dias.ToDictionary(d => d.Dia, d => d.Turno));

    public int DiaDelCiclo(DateOnly fecha) =>
        (((fecha.DayNumber - fechaInicio.DayNumber) % diasDelCiclo) + diasDelCiclo) % diasDelCiclo + 1;

    public TurnoDelCuadroDeTurnos Para(DateOnly fecha) => turnos[DiaDelCiclo(fecha)];
}
