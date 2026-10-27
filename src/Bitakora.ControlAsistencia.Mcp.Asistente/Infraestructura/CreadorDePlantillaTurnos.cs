using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantilla;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

internal sealed record AsignacionDeDia(int Dia, string? TurnoId, FranjaInline? Franja);

internal sealed record MensajesDeCreacion(
    string RechazoDelDominio, string TurnosNoExisten, MensajesDeTurnoEnLinea EnLinea);

internal sealed record PlantillaCreada(
    Guid PlantillaId,
    int DiasAsignados,
    IReadOnlyList<DiaDeCicloRechazado> DiasRechazados,
    IReadOnlyList<TurnoInlineResumen> TurnosEnLinea,
    IReadOnlyDictionary<int, string> TurnoPorDia,
    JornadaDescritaResumen? Jornada);

internal sealed record ResultadoCreacion(string? Rechazo, PlantillaCreada? Plantilla);

internal sealed class CreadorDePlantillaTurnos(
    ProgramacionApi programacion, ResolutorTurnoPorId resolutorPorId, ResolutorTurnoPorNombre resolutor)
{
    public Task<ResultadoCreacion> CrearAsync(
        string nombre, int diasDelCiclo, IReadOnlyList<AsignacionDeDia> asignaciones, Guid? jornadaId,
        MensajesDeCreacion mensajes, CancellationToken ct)
        => throw new NotImplementedException();
}
