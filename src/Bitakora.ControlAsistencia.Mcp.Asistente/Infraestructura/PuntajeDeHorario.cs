namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public enum TipoCoincidenciaDeHorario
{
    Exacto,
    Cercano,
    SeCruza
}

public sealed record CoincidenciaDeHorario(
    TipoCoincidenciaDeHorario Tipo,
    int DistanciaEnMinutos,
    FranjaFicha Franja);

public sealed class PuntajeDeHorario
{
    public static bool TryParsear(string? valor, out PuntajeDeHorario puntaje)
    {
        puntaje = null!;
        throw new NotImplementedException();
    }

    /// <summary>Mejor coincidencia entre las franjas, o null si el turno no entra.</summary>
    public CoincidenciaDeHorario? Evaluar(IReadOnlyList<FranjaFicha> franjas) =>
        throw new NotImplementedException();
}
