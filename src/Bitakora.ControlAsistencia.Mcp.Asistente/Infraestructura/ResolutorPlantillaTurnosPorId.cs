namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorPlantillaTurnosPorId(ProgramacionApi programacion)
{
    public Task<ResultadoResolucionPlantillaTurnosPorId> ResolverAsync(Guid plantillaId, CancellationToken ct) =>
        throw new NotImplementedException();
}

/// <summary>Exactamente uno de Ficha/FalloDeLectura/NoExiste viene poblado.</summary>
public sealed record ResultadoResolucionPlantillaTurnosPorId(CuadroDeTurnos? Ficha, string? FalloDeLectura, bool NoExiste);
