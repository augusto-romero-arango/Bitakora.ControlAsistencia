using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

public interface IValidadorTokenAuthKit
{
    Task<ClaimsPrincipal> ValidarAsync(string tokenBearer, CancellationToken cancellationToken = default);
}

// Valida el access token que AuthKit emite para este resource MCP (issue #554): issuer y firma
// contra el discovery doc client-specific (MEF-ADR-0032 B5, mismo client_01M1CKPECJ5DBRMS3ZVFRQW8GW
// verificado en vivo para el gateway), expiracion via TokenValidationParameters. La
// obtencion/cacheo del discovery doc vive en el IConfigurationManager inyectado -- nunca aqui --
// para que el unit test lo sustituya por un StaticConfigurationManager sin red real (MEF-ADR-0048
// seccion 1, "handler falso, sin red real").
public sealed class ValidadorTokenAuthKit(IConfigurationManager<OpenIdConnectConfiguration> configuracion)
    : IValidadorTokenAuthKit
{
    public Task<ClaimsPrincipal> ValidarAsync(string tokenBearer, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
