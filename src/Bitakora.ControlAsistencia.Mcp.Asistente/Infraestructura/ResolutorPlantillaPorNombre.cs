namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorPlantillaPorNombre(ProgramacionApi programacion)
{
    public const int MaximoPlantillasEnMensaje = 20;

    public Task<ResultadoResolucionPlantilla> ResolverAsync(string nombre, CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record ResultadoResolucionPlantilla(
    CuadroSemanalTurnos? Ficha,
    string? FalloDeLectura,
    IReadOnlyList<string> NombresDisponibles);
