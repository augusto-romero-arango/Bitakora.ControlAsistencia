using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerSesion;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ObtenerSesionToolTests
{
    private static readonly IdentidadTenant TenantFijo = new("tenant-fijo", "usuario-fijo", "membership-fijo");

    private static readonly SesionUsuario SesionCompleta =
        new("ana@acme.co", "org_acme", "Acme SAS", "om_123");

    private static ObtenerSesionTool Tool => new(TenantFijo);

    [Fact]
    public void ObtenerSesion_RespondeOrigenSesionConCorreoEmpresaYMembership_CuandoElTokenTraeNombreDeEmpresa()
    {
        using var json = JsonDocument.Parse(Tool.Describir(SesionCompleta));
        var raiz = json.RootElement;

        raiz.GetProperty("origen").GetString().Should().Be("sesion");
        raiz.GetProperty("correo").GetString().Should().Be("ana@acme.co");
        raiz.GetProperty("empresa").GetProperty("id").GetString().Should().Be("org_acme");
        raiz.GetProperty("empresa").GetProperty("nombre").GetString().Should().Be("Acme SAS");
        raiz.GetProperty("membership").GetString().Should().Be("om_123");
    }

    [Fact]
    public void ObtenerSesion_OmiteElNombreDeEmpresa_CuandoElTokenNoTraeOrganizationName()
    {
        var sesion = SesionCompleta with { OrganizacionNombre = null };

        using var json = JsonDocument.Parse(Tool.Describir(sesion));
        var empresa = json.RootElement.GetProperty("empresa");

        empresa.GetProperty("id").GetString().Should().Be("org_acme");
        empresa.TryGetProperty("nombre", out _).Should().BeFalse();
    }

    [Fact]
    public void ObtenerSesion_RespondeTenantFijoSinCorreoYConAdvertencia_CuandoNoHayIdentidadDelToken()
    {
        using var json = JsonDocument.Parse(Tool.Describir(null));
        var raiz = json.RootElement;

        raiz.GetProperty("origen").GetString().Should().Be("tenant_fijo");
        raiz.GetProperty("empresa").GetProperty("id").GetString().Should().Be("tenant-fijo");
        raiz.TryGetProperty("correo", out _).Should().BeFalse();
        raiz.GetRawText().Should().Contain(ObtenerSesionTool.Mensajes.SinSesionDeUsuario);
    }

    [Fact]
    public void ObtenerSesion_NoExponeSubSidNiExpiracion_CuandoHaySesion()
    {
        var json = Tool.Describir(SesionCompleta);

        json.Should().NotContain("\"sub\"").And.NotContain("\"sid\"").And.NotContain("\"exp\"");
    }
}
