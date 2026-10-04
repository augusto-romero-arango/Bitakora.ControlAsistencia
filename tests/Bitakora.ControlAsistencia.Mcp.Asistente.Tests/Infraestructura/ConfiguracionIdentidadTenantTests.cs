using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ConfiguracionIdentidadTenantTests
{
    [Fact]
    public void ConfigurarIdentidadTenant_RegistraElMembershipInterino_CuandoElSettingLlega()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Identidad:TenantIdInterino"] = "tenant-fijo-01",
            ["Identidad:UserIdInterino"] = "usuario-mcp",
            ["Identidad:OrganizationMembershipIdInterino"] = "om_fijo_01"
        }).Build();
        var services = new ServiceCollection().ConfigurarIdentidadTenant(configuracion);

        using var proveedor = services.BuildServiceProvider();
        var identidad = proveedor.GetRequiredService<IdentidadTenant>();

        identidad.OrganizationMembershipId.Should().Be("om_fijo_01");
        identidad.TenantId.Should().Be("tenant-fijo-01");
    }
}
