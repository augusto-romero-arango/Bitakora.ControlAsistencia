using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// CA-2/CA-4: validacion real de issuer, firma (contra JWKS) y expiracion, sin red -- el discovery
// doc llega via StaticConfigurationManager (mismo tipo que produccion inyectaria un
// ConfigurationManager<OpenIdConnectConfiguration> real, MEF-ADR-0048 seccion 1). El issuer
// client-specific replica el confirmado en vivo para el gateway (MEF-ADR-0032 B5).
public class ValidadorTokenAuthKitTests
{
    private const string IssuerAuthKit =
        "https://api.workos.com/user_management/client_01M1CKPECJ5DBRMS3ZVFRQW8GW";

    private static IConfigurationManager<OpenIdConnectConfiguration> ConfiguracionCon(RsaSecurityKey llave)
    {
        var configuracion = new OpenIdConnectConfiguration { Issuer = IssuerAuthKit };
        configuracion.SigningKeys.Add(llave);
        return new StaticConfigurationManager<OpenIdConnectConfiguration>(configuracion);
    }

    private static RsaSecurityKey CrearLlave(string keyId)
    {
        var rsa = RSA.Create(2048);
        return new RsaSecurityKey(rsa) { KeyId = keyId };
    }

    private static string CrearToken(string issuer, RsaSecurityKey llaveDeFirma, DateTime expira)
    {
        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: null,
            subject: new ClaimsIdentity([new Claim("sub", "usuario-mcp")]),
            notBefore: DateTime.UtcNow.AddMinutes(-5),
            expires: expira,
            issuedAt: DateTime.UtcNow,
            signingCredentials: new SigningCredentials(llaveDeFirma, SecurityAlgorithms.RsaSha256));
        return handler.WriteToken(token);
    }

    [Fact]
    public async Task ValidarAsync_RetornaClaimsPrincipal_CuandoElTokenEsValido()
    {
        var llave = CrearLlave("kid-authkit-vigente");
        var validador = new ValidadorTokenAuthKit(ConfiguracionCon(llave));
        var token = CrearToken(IssuerAuthKit, llave, DateTime.UtcNow.AddMinutes(10));

        var principal = await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        principal.Should().NotBeNull();
        principal.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "usuario-mcp");
    }

    [Fact]
    public async Task ValidarAsync_LanzaSecurityTokenExpiredException_CuandoElTokenExpiro()
    {
        var llave = CrearLlave("kid-authkit-vigente");
        var validador = new ValidadorTokenAuthKit(ConfiguracionCon(llave));
        var token = CrearToken(IssuerAuthKit, llave, DateTime.UtcNow.AddMinutes(-10));

        var act = async () => await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<SecurityTokenExpiredException>();
    }

    [Fact]
    public async Task ValidarAsync_LanzaSecurityTokenInvalidIssuerException_CuandoElEmisorNoCoincideConElDiscoveryDoc()
    {
        var llave = CrearLlave("kid-authkit-vigente");
        var validador = new ValidadorTokenAuthKit(ConfiguracionCon(llave));
        var token = CrearToken("https://issuer-impostor.example.com", llave, DateTime.UtcNow.AddMinutes(10));

        var act = async () => await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<SecurityTokenInvalidIssuerException>();
    }

    [Fact]
    public async Task ValidarAsync_LanzaSecurityTokenSignatureKeyNotFoundException_CuandoLaFirmaNoCoincideConNingunaLlaveDelJwks()
    {
        var llaveConocidaPorElValidador = CrearLlave("kid-authkit-vigente");
        var llaveImpostora = CrearLlave("kid-impostor");
        var validador = new ValidadorTokenAuthKit(ConfiguracionCon(llaveConocidaPorElValidador));
        var token = CrearToken(IssuerAuthKit, llaveImpostora, DateTime.UtcNow.AddMinutes(10));

        var act = async () => await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        await act.Should().ThrowExactlyAsync<SecurityTokenSignatureKeyNotFoundException>();
    }
}
