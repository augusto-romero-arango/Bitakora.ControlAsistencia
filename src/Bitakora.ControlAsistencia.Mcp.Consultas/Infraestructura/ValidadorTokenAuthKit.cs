using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public interface IValidadorTokenAuthKit
{
    Task<ClaimsPrincipal> ValidarAsync(string tokenBearer, CancellationToken cancellationToken = default);
}

// Valida el access token que AuthKit emite para este resource MCP (issue #554): issuer y firma
// contra el discovery doc del dominio AuthKit del entorno -- no el issuer de LOGIN del gateway
// (issue #560, ver Program.cs) --, expiracion via TokenValidationParameters. La
// obtencion/cacheo del discovery doc vive en el IConfigurationManager inyectado -- nunca aqui --
// para que el unit test lo sustituya por un StaticConfigurationManager sin red real (MEF-ADR-0048
// seccion 1, "handler falso, sin red real").
public sealed class ValidadorTokenAuthKit(IConfigurationManager<OpenIdConnectConfiguration> configuracion)
    : IValidadorTokenAuthKit
{
    // MapInboundClaims=false: sin esto, el handler traduce "sub" al URI largo de
    // ClaimTypes.NameIdentifier (mapeo heredado de WS-Federation) antes de que #540 pueda leerlo.
    private static readonly JwtSecurityTokenHandler Handler = new() { MapInboundClaims = false };

    public async Task<ClaimsPrincipal> ValidarAsync(string tokenBearer, CancellationToken cancellationToken = default)
    {
        var discoveryDoc = await configuracion.GetConfigurationAsync(cancellationToken);

        var parametrosValidacion = new TokenValidationParameters
        {
            ValidIssuer = discoveryDoc.Issuer,
            ValidateIssuer = true,
            IssuerSigningKeys = discoveryDoc.SigningKeys,
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            ValidateLifetime = true,
        };

        return Handler.ValidateToken(tokenBearer, parametrosValidacion, out _);
    }
}
