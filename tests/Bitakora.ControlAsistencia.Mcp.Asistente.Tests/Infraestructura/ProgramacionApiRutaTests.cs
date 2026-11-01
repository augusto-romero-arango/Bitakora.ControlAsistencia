using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ProgramacionApiRutaTests
{
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private static SolicitudProgramacionTurno Solicitud() => new(
        Guid.Parse("8f14e45f-ceea-4b3c-8f0a-0000000000aa"),
        Guid.Parse("8f14e45f-ceea-4b3c-8f0a-000000000001"),
        new ColaboradorSolicitado("1111", "C-1", "Ana Perez"),
        [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)],
        null,
        null);

    private static async Task<HandlerEnlatado> EnviarDesdeElHostCompuesto(string? valorSetting)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Programacion:BaseUrl"] = "https://programacion.falso.local",
                ["Api:Sedes:BaseUrl"] = "https://sedes.falso.local",
                ["Api:Colaboradores:BaseUrl"] = "https://colaboradores.falso.local",
                ["Api:ControlHoras:BaseUrl"] = "https://controlhoras.falso.local",
                ["Programacion:SolicitudEnFechas"] = valorSetting
            })
            .Build();
        var handler = new HandlerEnlatado(HttpStatusCode.Created, "{}");
        var services = new ServiceCollection().ConfigurarServiciosMcp(configuracion);
        services.AddHttpClient<ProgramacionApi>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var proveedor = services.BuildServiceProvider();
        using var alcance = proveedor.CreateScope();

        await alcance.ServiceProvider.GetRequiredService<ProgramacionApi>()
            .SolicitarProgramacion(Solicitud(), TestContext.Current.CancellationToken);
        return handler;
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaSolicitudes_CuandoElSettingEstaAusente()
    {
        var handler = await EnviarDesdeElHostCompuesto(null);

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be(RutaSolicitudes);
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaSolicitudes_CuandoElSettingEsFalse()
    {
        var handler = await EnviarDesdeElHostCompuesto("false");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaSolicitudes);
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaSolicitudesSinFallar_CuandoElSettingNoEsBooleano()
    {
        var handler = await EnviarDesdeElHostCompuesto("talvez");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaSolicitudes);
    }
}
