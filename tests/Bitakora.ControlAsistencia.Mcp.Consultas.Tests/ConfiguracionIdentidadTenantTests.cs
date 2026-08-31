using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ConfiguracionIdentidadTenantTests
{
    [Fact]
    public void Leer_RetornaLaIdentidad_CuandoAmbosValoresLlegan()
    {
        var identidad = ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "usuario-mcp");

        identidad.TenantId.Should().Be("tenant-fijo-01");
        identidad.UserId.Should().Be("usuario-mcp");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoFaltaTenantId()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer(null, "usuario-mcp");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.TenantIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoTenantIdEsBlanco()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("   ", "usuario-mcp");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.TenantIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoFaltaUserId()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", null);

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.UserIdAusente}*");
    }
}
