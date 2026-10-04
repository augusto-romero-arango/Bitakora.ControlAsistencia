using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;

/// <summary>Turno del molde que corresponde a cada fecha, con la semana 1 alineada a la semana que contiene 'desde'.</summary>
internal sealed class AsignacionDePlantilla
{
    private AsignacionDePlantilla()
    {
    }

    public static AsignacionDePlantilla Crear(CuadroSemanalTurnos cuadro, DateOnly desde) =>
        throw new NotImplementedException();

    public TurnoDelCuadro Para(DateOnly fecha) => throw new NotImplementedException();
}
