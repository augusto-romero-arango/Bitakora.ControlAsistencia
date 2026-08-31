namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Lee la identidad de tenant interina desde app settings, con el mismo criterio fail-fast que
/// <c>LeerBaseUrl</c> en Program.cs: el arranque falla si falta un valor, nunca la primera tool
/// call.
/// </summary>
public sealed partial class ConfiguracionIdentidadTenant
{
    public static IdentidadTenant Leer(string? tenantId, string? userId) =>
        throw new NotImplementedException();
}
