using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerSesion;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ObtenerSesionToolRunTests
{
    private static readonly IdentidadTenant TenantFijo = new("tenant-fijo", "usuario-fijo", "membership-fijo");

    private static readonly ToolInvocationContext Invocacion = new() { Name = "obtener_sesion", SessionId = "s-1" };

    [Fact]
    public async Task ObtenerSesion_RespondeOrigenSesion_CuandoLosItemsTraenLaSesionDelUsuario()
    {
        var contexto = new FunctionContextFake();
        contexto.Items[SesionUsuario.ClaveItems] = new SesionUsuario("ana@acme.co", "org_acme", "Acme SAS", "om_123");

        var respuesta = await new ObtenerSesionTool(TenantFijo).Run(Invocacion, contexto);

        using var json = JsonDocument.Parse(respuesta);
        json.RootElement.GetProperty("origen").GetString().Should().Be("sesion");
        json.RootElement.GetProperty("correo").GetString().Should().Be("ana@acme.co");
        json.RootElement.GetProperty("empresa").GetProperty("id").GetString().Should().Be("org_acme");
    }

    [Fact]
    public async Task ObtenerSesion_RespondeTenantFijo_CuandoLosItemsNoTraenSesion()
    {
        var respuesta = await new ObtenerSesionTool(TenantFijo).Run(Invocacion, new FunctionContextFake());

        using var json = JsonDocument.Parse(respuesta);
        json.RootElement.GetProperty("origen").GetString().Should().Be("tenant_fijo");
        json.RootElement.GetProperty("empresa").GetProperty("id").GetString().Should().Be("tenant-fijo");
    }

    [Fact]
    public async Task ObtenerSesion_RespondeTenantFijo_CuandoElItemNoEsUnaSesionDeUsuario()
    {
        var contexto = new FunctionContextFake();
        contexto.Items[SesionUsuario.ClaveItems] = "no-es-sesion";

        var respuesta = await new ObtenerSesionTool(TenantFijo).Run(Invocacion, contexto);

        using var json = JsonDocument.Parse(respuesta);
        json.RootElement.GetProperty("origen").GetString().Should().Be("tenant_fijo");
    }

    private sealed class FunctionContextFake : FunctionContext
    {
        public override string InvocationId => "inv-1";
        public override string FunctionId => "fn-1";
        public override TraceContext TraceContext => throw new NotSupportedException();
        public override BindingContext BindingContext => throw new NotSupportedException();
        public override RetryContext RetryContext => throw new NotSupportedException();
        public override IServiceProvider InstanceServices { get; set; } = null!;
        public override FunctionDefinition FunctionDefinition => throw new NotSupportedException();
        public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();
        public override IInvocationFeatures Features => throw new NotSupportedException();
    }
}
