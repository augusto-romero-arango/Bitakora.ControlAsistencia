using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ConfiguracionIdentidadTenantTests
{
    [Fact]
    public void Leer_RetornaLaIdentidad_CuandoAmbosValoresLlegan()
    {
        var identidad = ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "usuario-mcp", "membership-fijo");

        identidad.TenantId.Should().Be("tenant-fijo-01");
        identidad.UserId.Should().Be("usuario-mcp");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoFaltaTenantId()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer(null, "usuario-mcp", "membership-fijo");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.TenantIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoTenantIdEsBlanco()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("   ", "usuario-mcp", "membership-fijo");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.TenantIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoFaltaUserId()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", null, "membership-fijo");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.UserIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoUserIdEsBlanco()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "   ", "membership-fijo");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.UserIdAusente}*");
    }

    [Fact]
    public void Leer_RetornaElMembershipId_CuandoLosTresValoresLlegan()
    {
        var identidad = ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "usuario-mcp", "om_fijo_01");

        identidad.OrganizationMembershipId.Should().Be("om_fijo_01");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoFaltaMembershipId()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "usuario-mcp", null);

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.OrganizationMembershipIdAusente}*");
    }

    [Fact]
    public void Leer_LanzaInvalidOperationException_CuandoMembershipIdEstaEnBlanco()
    {
        var act = () => ConfiguracionIdentidadTenant.Leer("tenant-fijo-01", "usuario-mcp", "   ");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage($"*{ConfiguracionIdentidadTenant.Mensajes.OrganizationMembershipIdAusente}*");
    }
}
