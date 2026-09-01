using System.Security.Claims;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

/// <summary>
/// Fake manual (nunca NSubstitute) de <see cref="IValidadorTokenAuthKit"/>: aisla la decision
/// 401/pasa del middleware de la criptografia real del token, ya cubierta por
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

    public Task<ClaimsPrincipal> ValidarAsync(string tokenBearer, CancellationToken cancellationToken = default) =>
        _excepcion is not null
            ? Task.FromException<ClaimsPrincipal>(_excepcion)
            : Task.FromResult(_principal!);
}
