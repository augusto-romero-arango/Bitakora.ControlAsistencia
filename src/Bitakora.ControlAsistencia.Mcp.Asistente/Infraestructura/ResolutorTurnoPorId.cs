namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorTurnoPorId
{
    public ResolutorTurnoPorId(ProgramacionApi programacion)
    {
    }

    public Task<ResultadoResolucionTurno> ResolverAsync(string turnoId, CancellationToken ct) =>
        throw new NotImplementedException();
}
