using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.CerrarSesion;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests;

public class CerrarSesionToolTests
{
    private const string SidDePrueba = "sesion 01+A/B";
    private const string SidEscapado = "sesion%2001%2BA%2FB";
    private const string UrlEsperada =
        "https://api.workos.com/user_management/sessions/logout?session_id=" + SidEscapado;

    private static readonly ToolInvocationContext Invocacion = new() { Name = "cerrar_sesion", SessionId = "s-1" };

    private static CerrarSesionTool Tool => new();

    [Fact]
    public void CerrarSesion_RespondeLaUrlDeLogoutConElSidEscapado_CuandoLaSesionTraeSid()
    {
        using var json = JsonDocument.Parse(Tool.Describir(new SesionUsuario("org_acme", "om_123", SidDePrueba)));
        var raiz = json.RootElement;

        raiz.GetProperty("url").GetString().Should().Be(UrlEsperada);
        raiz.GetProperty("resultado").GetString().Should().Be(CerrarSesionTool.Mensajes.AbreElEnlace);
        raiz.GetProperty("nota").GetString().Should().Be(CerrarSesionTool.Mensajes.NotaCierre);
    }

    [Fact]
    public void CerrarSesion_ExplicaVencimientoDelTokenYEleccionDeEmpresa_CuandoLaSesionTraeSid()
    {
        using var json = JsonDocument.Parse(Tool.Describir(new SesionUsuario("org_acme", "om_123", SidDePrueba)));
        var nota = json.RootElement.GetProperty("nota").GetString();

        nota.Should().Contain("token").And.Contain("minutos").And.Contain("empresa");
    }

    [Fact]
    public void CerrarSesion_NoExponeElSidFueraDeLaUrl_CuandoLaSesionTraeSid()
    {
        using var json = JsonDocument.Parse(Tool.Describir(new SesionUsuario("org_acme", "om_123", SidDePrueba)));
        var raiz = json.RootElement;

        raiz.GetProperty("resultado").GetString().Should().NotContain("01");
        raiz.GetProperty("nota").GetString().Should().NotContain("01");
    }

    [Fact]
    public void CerrarSesion_RespondeSinSesionYSinUrl_CuandoNoHaySesionDeUsuario()
    {
        using var json = JsonDocument.Parse(Tool.Describir(null));
        var raiz = json.RootElement;

        raiz.GetProperty("resultado").GetString().Should().Be(CerrarSesionTool.Mensajes.SinSesionQueCerrar);
        raiz.TryGetProperty("url", out _).Should().BeFalse();
    }

    [Fact]
    public void CerrarSesion_RespondeSinSesionYSinUrl_CuandoElTokenNoTraeSid()
    {
        using var json = JsonDocument.Parse(Tool.Describir(new SesionUsuario("org_acme", "om_123")));
        var raiz = json.RootElement;

        raiz.GetProperty("resultado").GetString().Should().Be(CerrarSesionTool.Mensajes.SinSesionQueCerrar);
        raiz.TryGetProperty("url", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CerrarSesion_RespondeLaUrl_CuandoLosItemsTraenLaSesionConSid()
    {
        var contexto = new FunctionContextFake();
        contexto.Items[SesionUsuario.ClaveItems] = new SesionUsuario("org_acme", "om_123", SidDePrueba);

        var respuesta = await Tool.Run(Invocacion, contexto);

        using var json = JsonDocument.Parse(respuesta);
        json.RootElement.GetProperty("url").GetString().Should().Be(UrlEsperada);
    }

    [Fact]
    public async Task CerrarSesion_RespondeSinSesion_CuandoLosItemsNoTraenSesion()
    {
        var respuesta = await Tool.Run(Invocacion, new FunctionContextFake());

        using var json = JsonDocument.Parse(respuesta);
        json.RootElement.GetProperty("resultado").GetString().Should().Be(CerrarSesionTool.Mensajes.SinSesionQueCerrar);
        json.RootElement.TryGetProperty("url", out _).Should().BeFalse();
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
