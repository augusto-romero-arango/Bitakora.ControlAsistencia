using System.Net;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Infraestructura;

public class ProgramacionApiInterruptorTests
{
    private static readonly Guid TurnoId = Guid.Parse("8f14e45f-ceea-4b3c-8f0a-000000000001");

    private static SolicitudProgramacionTurno Solicitud() => new(
        Guid.Parse("8f14e45f-ceea-4b3c-8f0a-0000000000aa"),
        TurnoId,
        new ColaboradorSolicitado("1111", "C-1", "Ana Perez"),
        [new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 2)],
        null,
        null);

    private static async Task<(HttpRequestMessage Request, string? Cuerpo)> Enviar(OpcionesProgramacion? opciones)
    {
        var handler = new HandlerEnlatado(HttpStatusCode.Created, "{}");
        var cliente = new HttpClient(handler) { BaseAddress = new Uri("https://programacion.falso.local") };
        await new ProgramacionApi(cliente, opciones).SolicitarProgramacion(Solicitud(), TestContext.Current.CancellationToken);
        return (handler.UltimaRequest!, handler.UltimoCuerpoEnviado);
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaEnFechas_CuandoElInterruptorEstaPrendido()
    {
        var (request, cuerpo) = await Enviar(new OpcionesProgramacion(SolicitudEnFechas: true));

        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().Be("/api/programacion/solicitudes-en-fechas");
        cuerpo.Should().Contain("\"colaborador\"").And.Contain("2026-09-01");
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaPorDia_CuandoElInterruptorEstaApagado()
    {
        var (request, _) = await Enviar(new OpcionesProgramacion(SolicitudEnFechas: false));

        request.RequestUri!.AbsolutePath.Should().Be("/api/programacion/solicitudes");
    }

    [Fact]
    public async Task SolicitarProgramacion_EnviaARutaPorDia_CuandoNoHayOpciones()
    {
        var (request, _) = await Enviar(null);

        request.RequestUri!.AbsolutePath.Should().Be("/api/programacion/solicitudes");
    }

    private static ServiceProvider Componer(string? valorInterruptor)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Api:Programacion:BaseUrl"] = "https://programacion.falso.local",
            ["Api:Sedes:BaseUrl"] = "https://sedes.falso.local",
            ["Api:Colaboradores:BaseUrl"] = "https://colaboradores.falso.local",
            ["Api:ControlHoras:BaseUrl"] = "https://controlhoras.falso.local",
            ["Programacion:SolicitudEnFechas"] = valorInterruptor
        };
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.ConfigurarClientesHttp(configuracion);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigurarClientesHttp_FallaNombrandoElSetting_CuandoElValorNoEsBooleano()
    {
        var act = () => Componer("quizas");

        act.Should().ThrowExactly<InvalidOperationException>()
            .WithMessage("*Programacion__SolicitudEnFechas*");
    }

    [Fact]
    public void ConfigurarClientesHttp_Compone_CuandoElSettingEstaAusente()
    {
        var act = () => Componer(null);

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_InterpretaElCreatedIgual_CuandoElInterruptorEstaPrendido()
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        var turnos = Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");
        const string turnoId = "8f14e45f-ceea-4b3c-8f0a-000000000001";
        handlerProgramacion.RespondeDetalleDeTurno(turnos, turnoId);
        handlerProgramacion.Responde(
            HttpMethod.Post, "/api/programacion/solicitudes-en-fechas", HttpStatusCode.Created,
            """{"fechasRespetadas":[{"fecha":"2026-09-13","motivo":"Vacaciones"}]}""");
        var (clienteSedes, _) = ClienteFalso.Con(Fixtures.Leer("SolicitarProgramacionTurno", "sede.json"));
        var (clienteColaboradores, _) = ClienteFalso.Con(Fixtures.Leer("SolicitarProgramacionTurno", "directorio.json"));
        var api = new ProgramacionApi(clienteProgramacion, new OpcionesProgramacion(true));
        var tool = new SolicitarProgramacionTurnoTool(
            api, new SedesApi(clienteSedes), new ResolutorTurnoPorId(api),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorLista(new ColaboradoresApi(clienteColaboradores)));

        var resultado = await tool.Run(
            context: null!, desde: "2026-09-01", hasta: "2026-09-30", turnoId: turnoId,
            sedeDeProgramacion: "SUBA", identificaciones: "CC-1111", ct: TestContext.Current.CancellationToken);

        handlerProgramacion.Requests.Should().Contain(r => r.Ruta == "/api/programacion/solicitudes-en-fechas");
        resultado.Should().Contain("\"dias\":29").And.Contain("Vacaciones");
    }
}
