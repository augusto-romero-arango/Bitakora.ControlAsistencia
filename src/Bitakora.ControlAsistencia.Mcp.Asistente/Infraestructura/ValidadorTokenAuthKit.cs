using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

public interface IValidadorTokenAuthKit
{
    Task<bool> EsValidoAsync(string token, CancellationToken ct);

    Task<ClaimsPrincipal?> ValidarAsync(string token, CancellationToken ct);
}

/// <summary>
/// Defensa en profundidad, nunca el gate primario: ese es la politica de APIM (MEF-ADR-0032 seccion 9),
/// que ya exige la audiencia; por eso ValidateAudience = false. Authority = dominio AuthKit del entorno,
/// nunca el issuer de login user_management/{client_id} (re-verificar contra el discovery doc en vivo).
/// </summary>
public sealed class ValidadorTokenAuthKit(IConfigurationManager<OpenIdConnectConfiguration>? configManager)
    : IValidadorTokenAuthKit
{
    // Sin authorization server resoluble (app setting ausente o aun el placeholder que siembra Terraform)
    // el validador degrada a "todo token es invalido", nunca a una excepcion de arranque.
    public static ValidadorTokenAuthKit ParaAuthorizationServer(string? authorizationServer) =>
        Uri.TryCreate(authorizationServer, UriKind.Absolute, out var autoridad)
            ? new ValidadorTokenAuthKit(new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{autoridad.ToString().TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()))
            : new ValidadorTokenAuthKit(configManager: null);

    // MapInboundClaims=false: sin esto el handler traduce "sub" al URI largo de
    // ClaimTypes.NameIdentifier antes de que el derivador pueda leerlo.
    private static readonly JwtSecurityTokenHandler Handler = new() { MapInboundClaims = false };

    public async Task<bool> EsValidoAsync(string token, CancellationToken ct) =>
        await ValidarAsync(token, ct) is not null;

    public async Task<ClaimsPrincipal?> ValidarAsync(string token, CancellationToken ct)
    {
        if (configManager is null)
            return null;

        try
        {
            var config = await configManager.GetConfigurationAsync(ct);
            var parametros = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = config.Issuer,
                ValidateAudience = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = config.SigningKeys,
                ValidateLifetime = true
            };

            return Handler.ValidateToken(token, parametros, out _);
        }
        catch (Exception)
        {
            // Cualquier fallo es "no valido" y nunca propaga: este validador jamas debe tumbar el pipeline.
            return null;
        }
    }
}
