namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorPlantillaPorId(ProgramacionApi programacion)
{
    public Task<ResultadoResolucionPlantillaPorId> ResolverAsync(Guid plantillaId, CancellationToken ct)
    {
        _ = programacion;
        throw new NotImplementedException();
    }
}

/// <summary>Exactamente uno de Ficha/FalloDeLectura/NoExiste viene poblado.</summary>
public sealed record ResultadoResolucionPlantillaPorId(CuadroSemanalTurnos? Ficha, string? FalloDeLectura, bool NoExiste);
