using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;

public interface IValidadorTokenAuthKit
{
    Task<bool> EsValidoAsync(string token, CancellationToken ct);

    Task<ClaimsPrincipal?> ValidarAsync(string token, CancellationToken ct);
}

/// <summary>
/// Validador de token de defensa en profundidad (MEF-ADR-0047 decision 7): nunca el gate primario
/// -- ese vive en la politica dedicada de APIM (MEF-ADR-0032 seccion 9). ValidateAudience = false
/// porque la audiencia ya la exige esa politica antes de que el request llegue a este worker.
/// Authority = dominio AuthKit del entorno (MEF-ADR-0032 B12), nunca el issuer de login
/// user_management/{client_id} -- re-verificar contra el discovery doc en vivo por consumidor.
/// </summary>
public sealed class ValidadorTokenAuthKit(IConfigurationManager<OpenIdConnectConfiguration>? configManager)
    : IValidadorTokenAuthKit
{
    // Sin authorization server resoluble -- app setting ausente, o todavia el placeholder que el
    // Terraform siembra hasta que existe el API de APIM del servidor -- el validador degrada a
    // "todo token es invalido", nunca a una excepcion de arranque (MEF-ADR-0047 decision 7).
    public static ValidadorTokenAuthKit ParaAuthorizationServer(string? authorizationServer) =>
        Uri.TryCreate(authorizationServer, UriKind.Absolute, out var autoridad)
            ? new ValidadorTokenAuthKit(new ConfigurationManager<OpenIdConnectConfiguration>(
                $"{autoridad.ToString().TrimEnd('/')}/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()))
            : new ValidadorTokenAuthKit(configManager: null);

    public async Task<bool> EsValidoAsync(string token, CancellationToken ct)
    {
        if (configManager is null)
            return false;

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

            new JwtSecurityTokenHandler().ValidateToken(token, parametros, out _);
            return true;
        }
        catch (Exception)
        {
            // Defensa en profundidad: cualquier fallo (token malformado, discovery doc no
            // alcanzable, firma invalida) se trata como "no valido", nunca propaga -- este
            // validador jamas debe tumbar el pipeline (MEF-ADR-0047 decision 7).
            return false;
        }
    }

    public Task<ClaimsPrincipal?> ValidarAsync(string token, CancellationToken ct) =>
        throw new NotImplementedException();
}
