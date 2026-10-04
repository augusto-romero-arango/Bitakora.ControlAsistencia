using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

/// <summary>Turno del molde que corresponde a cada fecha, con la semana 1 alineada a la semana que contiene 'desde'.</summary>
internal sealed class AsignacionDePlantilla
{
    private const int DiasPorSemana = 7;

    private readonly int semanas;
    private readonly DateOnly lunesInicial;
    private readonly IReadOnlyDictionary<(int Semana, int Dia), TurnoDelCuadro> turnos;

    private AsignacionDePlantilla(
        int semanas, DateOnly lunesInicial, IReadOnlyDictionary<(int Semana, int Dia), TurnoDelCuadro> turnos)
    {
        this.semanas = semanas;
        this.lunesInicial = lunesInicial;
        this.turnos = turnos;
    }

    public static AsignacionDePlantilla Crear(CuadroSemanalTurnos cuadro, DateOnly desde) =>
        new(
            cuadro.Semanas,
            LunesDe(desde),
            cuadro.Dias.ToDictionary(d => (d.Semana, d.Dia), d => d.Turno));

    public TurnoDelCuadro Para(DateOnly fecha)
    {
        var semanasTranscurridas = (LunesDe(fecha).DayNumber - lunesInicial.DayNumber) / DiasPorSemana;
        var semana = (((semanasTranscurridas % semanas) + semanas) % semanas) + 1;
        return turnos[(semana, DiaIso(fecha))];
    }

    private static int DiaIso(DateOnly fecha) => fecha.DayOfWeek == DayOfWeek.Sunday ? DiasPorSemana : (int)fecha.DayOfWeek;

    private static DateOnly LunesDe(DateOnly fecha) => fecha.AddDays(1 - DiaIso(fecha));
}
