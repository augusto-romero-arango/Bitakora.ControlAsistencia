using System.Security.Claims;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

/// <summary>
/// Fake manual (nunca NSubstitute) de <see cref="IDerivadorIdentidadTenantMcp"/>: aisla
/// <c>IdentidadTenantMcpMiddleware</c> de la traduccion real de claims, ya cubierta por
/// <c>DerivadorIdentidadTenantMcpTests</c>.
/// </summary>
public sealed class DerivadorIdentidadTenantMcpFalso : IDerivadorIdentidadTenantMcp
{
    private readonly IdentidadTenant? _identidad;
    private readonly Exception? _excepcion;

    private DerivadorIdentidadTenantMcpFalso(IdentidadTenant? identidad, Exception? excepcion)
    {
        _identidad = identidad;
        _excepcion = excepcion;
    }

    public static DerivadorIdentidadTenantMcpFalso QueDeriva(IdentidadTenant identidad) => new(identidad, null);

    public static DerivadorIdentidadTenantMcpFalso QueFalla(Exception excepcion) => new(null, excepcion);

    public IdentidadTenant Derivar(ClaimsPrincipal principal) =>
        _excepcion is not null ? throw _excepcion : _identidad!;
}
