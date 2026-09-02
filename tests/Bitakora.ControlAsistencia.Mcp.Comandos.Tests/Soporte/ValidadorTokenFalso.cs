using System.Security.Claims;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

/// <summary>
/// Fake manual (nunca NSubstitute) de <see cref="IValidadorTokenAuthKit"/>: aisla la orquestacion
/// de <c>IdentidadTenantMcpMiddleware</c> de la criptografia real del token, ya cubierta por
/// <c>ValidadorTokenAuthKitTests</c>.
/// </summary>
public sealed class ValidadorTokenFalso : IValidadorTokenAuthKit
{
    private readonly ClaimsPrincipal? _principal;
    private readonly Exception? _excepcion;

    private ValidadorTokenFalso(ClaimsPrincipal? principal, Exception? excepcion)
    {
        _principal = principal;
        _excepcion = excepcion;
    }

    public static ValidadorTokenFalso QueAutoriza(ClaimsPrincipal principal) => new(principal, null);

    public static ValidadorTokenFalso QueFalla(Exception excepcion) => new(null, excepcion);

    public Task<bool> EsValidoAsync(string token, CancellationToken ct) =>
        Task.FromResult(_excepcion is null);

    public Task<ClaimsPrincipal?> ValidarAsync(string token, CancellationToken ct) =>
        _excepcion is not null
            ? Task.FromException<ClaimsPrincipal?>(_excepcion)
            : Task.FromResult(_principal);
}
