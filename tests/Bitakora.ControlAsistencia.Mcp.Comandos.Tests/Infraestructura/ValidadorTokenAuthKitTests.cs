using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

public class ValidadorTokenAuthKitTests
{
    private const string IssuerAuthKit = "https://marvelous-polaroid-97-staging.authkit.app";

    [Fact]
    public async Task EsValidoAsync_DevuelveFalso_CuandoElTokenEstaMalformado()
    {
        var validador = new ValidadorTokenAuthKit(new ConfigManagerFalso());

        var esValido = await validador.EsValidoAsync("no-es-un-jwt", TestContext.Current.CancellationToken);

        esValido.Should().BeFalse("defensa en profundidad: nunca debe lanzar, solo degradar a invalido");
    }

    [Fact]
    public async Task ParaAuthorizationServer_NoLanzaYRechazaTodo_CuandoElAppSettingSigueEnPlaceholder()
    {
        var validador = ValidadorTokenAuthKit.ParaAuthorizationServer("PENDIENTE-DOMINIO-AUTHKIT-DEL-ENTORNO");

        var esValido = await validador.EsValidoAsync("cualquier-token", TestContext.Current.CancellationToken);

        esValido.Should().BeFalse("el placeholder del Terraform no puede tumbar el arranque del worker");
    }

    // CA-4: ValidarAsync es la version que el derivador de identidad (issue #572) necesita para leer
    // org_id/sub del ClaimsPrincipal -- EsValidoAsync solo informa si-es-valido, sin exponer los
    // claims. Mismo criterio fail-soft que EsValidoAsync: null en vez de lanzar.
    [Fact]
    public async Task ValidarAsync_RetornaClaimsPrincipal_CuandoElTokenEsValido()
    {
        var llave = CrearLlave("kid-authkit-vigente");
        var validador = new ValidadorTokenAuthKit(ConfiguracionCon(llave));
        var token = CrearToken(IssuerAuthKit, llave, DateTime.UtcNow.AddMinutes(10));

        var principal = await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        principal.Should().NotBeNull();
        principal!.Claims.Should().Contain(c => c.Type == "sub" && c.Value == "usuario-mcp");
    }

    [Fact]
    public async Task ValidarAsync_RetornaNull_CuandoElTokenEstaMalformado()
    {
        var validador = new ValidadorTokenAuthKit(new ConfigManagerFalso());

        var principal = await validador.ValidarAsync("no-es-un-jwt", TestContext.Current.CancellationToken);

        principal.Should().BeNull("defensa en profundidad: nunca debe lanzar, solo degradar a invalido");
    }

    [Fact]
    public async Task ValidarAsync_RetornaNull_CuandoNoHayAuthorizationServerConfigurado()
    {
        var validador = ValidadorTokenAuthKit.ParaAuthorizationServer("PENDIENTE-DOMINIO-AUTHKIT-DEL-ENTORNO");
        var llave = CrearLlave("kid-authkit-vigente");
        var token = CrearToken(IssuerAuthKit, llave, DateTime.UtcNow.AddMinutes(10));

        var principal = await validador.ValidarAsync(token, TestContext.Current.CancellationToken);

        principal.Should().BeNull("el placeholder del Terraform no puede tumbar el arranque del worker");
    }

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
            notBefore: expira.AddMinutes(-15),
            expires: expira,
            issuedAt: DateTime.UtcNow,
            signingCredentials: new SigningCredentials(llaveDeFirma, SecurityAlgorithms.RsaSha256));
        return handler.WriteToken(token);
    }
}

internal sealed class ConfigManagerFalso : IConfigurationManager<OpenIdConnectConfiguration>
{
    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
        Task.FromResult(new OpenIdConnectConfiguration { Issuer = "https://auth.falso.local" });

    public void RequestRefresh() { }
}
