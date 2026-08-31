namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

/// <summary>
/// Identidad de tenant interina leida de app settings. Falla en el arranque, nunca en la primera
/// tool call -- mismo criterio que <c>LeerBaseUrl</c> en Program.cs.
/// </summary>
public static partial class ConfiguracionIdentidadTenant
{
    public static IdentidadTenant Leer(string? tenantId, string? userId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new InvalidOperationException(Mensajes.TenantIdAusente);
        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException(Mensajes.UserIdAusente);

        return new IdentidadTenant(tenantId, userId);
    }
}
