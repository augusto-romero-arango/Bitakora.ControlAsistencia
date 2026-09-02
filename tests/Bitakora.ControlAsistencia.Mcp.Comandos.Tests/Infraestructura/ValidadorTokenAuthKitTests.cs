using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Infraestructura;

public class ValidadorTokenAuthKitTests
{
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
}

internal sealed class ConfigManagerFalso : IConfigurationManager<OpenIdConnectConfiguration>
{
    public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
        Task.FromResult(new OpenIdConnectConfiguration { Issuer = "https://auth.falso.local" });

    public void RequestRefresh() { }
}
