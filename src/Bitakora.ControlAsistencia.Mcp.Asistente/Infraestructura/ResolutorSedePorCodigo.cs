namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public sealed class ResolutorSedePorCodigo(SedesApi sedes)
{
    public Task<ResultadoResolucionSede> ResolverAsync(string codigo, CancellationToken ct) =>
        throw new NotImplementedException();
}

public sealed record ResultadoResolucionSede(
    SedeProgramada? Sede,
    string? FalloDeLectura,
    MotivoSedeNoResuelta? Motivo)
{
    public string? MensajeDelMotivo(string codigo, string noExiste, string inactiva) =>
        throw new NotImplementedException();
}

public enum MotivoSedeNoResuelta
{
    NoExiste,
    Inactiva
}
