using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ProgramacionApiInterruptorTests
{
    private const string RutaPorDia = "/api/programacion/solicitudes";
    private const string RutaEnFechas = "/api/programacion/solicitudes-en-fechas";

    private static SolicitudProgramacionTurno Solicitud() => new(
        Guid.Parse("8f14e45f-ceea-4b3c-8f0a-0000000000aa"),
        Guid.Parse("8f14e45f-ceea-4b3c-8f0a-000000000001"),
        new ColaboradorSolicitado("1111", "C-1", "Ana Perez"),
        [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)],
        null,
        null);

    private static async Task<HandlerEnlatado> Enviar(OpcionesProgramacion? opciones)
    {
        var handler = new HandlerEnlatado(HttpStatusCode.Created, "{}");
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://programacion.falso.local") };
        await new ProgramacionApi(cliente, opciones).SolicitarProgramacion(Solicitud(), TestContext.Current.CancellationToken);
        return handler;
    }

    private static IServiceCollection Componer(string? valorInterruptor)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:Programacion:BaseUrl"] = "https://programacion.falso.local",
                ["Api:Sedes:BaseUrl"] = "https://sedes.falso.local",
                ["Api:Colaboradores:BaseUrl"] = "https://colaboradores.falso.local",
                ["Api:ControlHoras:BaseUrl"] = "https://controlhoras.falso.local",
                ["Programacion:SolicitudEnFechas"] = valorInterruptor
            })
            .Build();

        return new ServiceCollection().ConfigurarServiciosMcp(configuracion);
    }

    private static async Task<HandlerEnlatado> EnviarDesdeElHostCompuesto(string? valorInterruptor)
    {
        var handler = new HandlerEnlatado(HttpStatusCode.Created, "{}");
        var services = Componer(valorInterruptor);
        services.AddHttpClient<ProgramacionApi>().ConfigurePrimaryHttpMessageHandler(() => handler);
        using var proveedor = services.BuildServiceProvider();
        using var alcance = proveedor.CreateScope();

        await alcance.ServiceProvider.GetRequiredService<ProgramacionApi>()
            .SolicitarProgramacion(Solicitud(), TestContext.Current.CancellationToken);
        return handler;
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaEnFechasConElMismoCuerpo_CuandoElInterruptorEstaPrendido()
    {
        var porDia = await Enviar(new OpcionesProgramacion(SolicitudEnFechas: false));

        var enFechas = await Enviar(new OpcionesProgramacion(SolicitudEnFechas: true));

        enFechas.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        enFechas.UltimaRequest.RequestUri!.AbsolutePath.Should().Be(RutaEnFechas);
        enFechas.UltimoCuerpoEnviado.Should().NotBeNullOrEmpty().And.Be(porDia.UltimoCuerpoEnviado);
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaPorDia_CuandoElInterruptorEstaApagado()
    {
        var handler = await Enviar(new OpcionesProgramacion(SolicitudEnFechas: false));

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be(RutaPorDia);
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaPorDia_CuandoNoHayOpciones()
    {
        var handler = await Enviar(null);

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaPorDia);
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaEnFechas_CuandoElSettingEsTrue()
    {
        var handler = await EnviarDesdeElHostCompuesto("true");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaEnFechas);
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaPorDia_CuandoElSettingEsFalse()
    {
        var handler = await EnviarDesdeElHostCompuesto("false");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaPorDia);
    }

    [Fact]
    public async Task ConfigurarServiciosMcp_EnrutaPorLaRutaPorDia_CuandoElSettingEstaAusente()
    {
        var handler = await EnviarDesdeElHostCompuesto(null);

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaPorDia);
    }

    [Fact]
    public void ConfigurarServiciosMcp_FallaNombrandoElSetting_CuandoElValorNoEsBooleano()
    {
        var act = () => Componer("quizas");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("*Programacion__SolicitudEnFechas*");
    }
}
