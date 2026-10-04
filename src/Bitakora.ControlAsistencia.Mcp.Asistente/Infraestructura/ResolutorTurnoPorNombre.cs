namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorTurnoPorNombre(ProgramacionApi programacion)
{
    public const int MaximoTurnosEnMensaje = 20;

    public Task<ResultadoResolucionTurno> ResolverAsync(string nombre, CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record ResultadoResolucionTurno(
    FichaTurno? Ficha,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
