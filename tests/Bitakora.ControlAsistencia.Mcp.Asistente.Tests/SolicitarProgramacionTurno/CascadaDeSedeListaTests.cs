using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurno;

public class CascadaDeSedeListaTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private sealed record Entorno(
        SolicitarProgramacionTurnoTool Tool,
        HandlerPorRuta Programacion,
        HandlerFuncional Sedes,
        LoggerDeCaptura<SolicitarProgramacionTurnoTool> Logger)
    {
        public List<JsonNode> Posts =>
            [.. Programacion.Requests
                .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
                .Select(r => JsonNode.Parse(r.Cuerpo!)!)];

        public JsonNode PostDe(string identificacion) =>
            Posts.Single(p => p["colaborador"]!["identificacion"]!.GetValue<string>() == identificacion);

        public List<HttpRequestMessage> LecturasDelMaestro =>
            [.. Sedes.Requests.Where(r => r.RequestUri!.AbsolutePath == EntornoDeSedes.RutaMaestroDeSedes)];
    }

    private static Entorno Crear(
        string turnosJson,
        string directorioJson,
        HttpStatusCode statusMaestro = HttpStatusCode.OK)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, turnosJson);
        programacion.Responde(HttpMethod.Post, RutaSolicitudes, HttpStatusCode.Created, "");
        var (clienteSedes, sedes) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r, statusMaestro));
        var (clienteColaboradores, _) = ClienteFalso.Con(directorioJson);

        var logger = new LoggerDeCaptura<SolicitarProgramacionTurnoTool>();
        var tool = new SolicitarProgramacionTurnoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores),
            logger);
        return new Entorno(tool, programacion, sedes, logger);
    }

    private static Task<string> Ejecutar(Entorno entorno, string? sedeDeProgramacion, string identificaciones) =>
        entorno.Tool.Run(
            context: null!,
            desde: "2026-09-01",
            hasta: "2026-09-03",
            turno: "Cocina Manana",
            sedeDeProgramacion: sedeDeProgramacion,
            identificaciones: identificaciones,
            ct: TestContext.Current.CancellationToken);

    // CA-2
    [Fact]
    public async Task SolicitarProgramacionTurno_EnviaLaSedeDelColaboradorResuelta_CuandoNoViajaSedeExplicita()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        var sede = entorno.PostDe("CC-1111")["sede"]!;
        sede["id"]!.GetValue<string>().Should().Be("SUBA");
        sede["nombre"]!.GetValue<string>().Should().Be("Sede Suba");
        sede["centroDeCostos"]!.GetValue<string>().Should().Be("CC-100");

        var json = JsonNode.Parse(resultado)!;
        json["sede"].Should().BeNull("no hubo sede explicita");
        json["programados"]!.AsArray().Single()!["sede"]!.GetValue<string>().Should().Be("SUBA");
        json.AsObject().ContainsKey("avisos").Should().BeFalse();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_LeeElMaestroDeSedesUnaSolaVezSinFiltro_CuandoHayVariosColaboradores()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(
                ("CC-1111", "AR01", "SUBA"), ("CC-2222", "BD01", "SUBA"), ("CC-3333", "CL01", "NORTE")));

        await Ejecutar(entorno, null, "CC-1111,CC-2222,CC-3333");

        entorno.LecturasDelMaestro.Should().ContainSingle();
        entorno.LecturasDelMaestro.Single().RequestUri!.Query.Should().NotContain("activa");
        entorno.Sedes.Requests.Should().OnlyContain(
            r => r.RequestUri!.AbsolutePath == EntornoDeSedes.RutaMaestroDeSedes,
            "nunca N lecturas de sedes/fichas/{codigo}");
    }

    // CA-3 / CA-4
    [Fact]
    public async Task SolicitarProgramacionTurno_ProgramaSinSedeYAvisaSinSedeAsignada_CuandoElColaboradorNoTieneSede()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", null)));

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        entorno.PostDe("CC-1111")["sede"].Should().BeNull();
        var json = JsonNode.Parse(resultado)!;
        var programado = json["programados"]!.AsArray().Single()!;
        programado.AsObject().ContainsKey("sede").Should().BeFalse("el default enviado fue null");
        json.AsObject().ContainsKey("fallidos").Should().BeFalse();
        var aviso = json["avisos"]!.AsArray().Single()!;
        aviso["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        aviso["motivo"]!.GetValue<string>().Should().Be(SolicitarProgramacionTurnoTool.Mensajes.AvisoSinSede);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_ProgramaSinSedeYAvisaSedeInactiva_CuandoLaSedeDelColaboradorEstaInactiva()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "NORTE")));

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        entorno.PostDe("CC-1111")["sede"].Should().BeNull();
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().ContainSingle();
        json["avisos"]!.AsArray().Single()!["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.AvisoSedeInactiva, "NORTE"));
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_ProgramaSinSedeYAvisaSedeNoExiste_CuandoLaSedeDelColaboradorNoEstaEnElMaestro()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "FANTASMA")));

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        entorno.PostDe("CC-1111")["sede"].Should().BeNull();
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().ContainSingle();
        json["avisos"]!.AsArray().Single()!["motivo"]!.GetValue<string>()
            .Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.AvisoSedeNoExiste, "FANTASMA"));
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_AvisaSoloALosColaboradoresSinSedeDisponible_CuandoElGrupoEsMixto()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA"), ("CC-2222", "BD01", null)));

        var resultado = await Ejecutar(entorno, null, "CC-1111,CC-2222");

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(2);
        json["avisos"]!.AsArray().Should().ContainSingle()
            .Which!["identificacion"]!.GetValue<string>().Should().Be("CC-2222");
    }

    // CA-1
    [Fact]
    public async Task SolicitarProgramacionTurno_EnviaLaSedeExplicitaATodos_CuandoViajaSedeDeProgramacion()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", null), ("CC-2222", "BD01", "NORTE")));

        var resultado = await Ejecutar(entorno, "SUBA", "CC-1111,CC-2222");

        entorno.Posts.Should().HaveCount(2)
            .And.OnlyContain(p => p["sede"]!["id"]!.GetValue<string>() == "SUBA");
        var json = JsonNode.Parse(resultado)!;
        json["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        json["programados"]!.AsArray().Should().AllSatisfy(
            p => p!.AsObject().ContainsKey("sede").Should().BeFalse("hubo sede explicita"));
        json.AsObject().ContainsKey("avisos").Should().BeFalse();
        entorno.LecturasDelMaestro.Should().BeEmpty("la explicita se valida por codigo, no por el maestro");
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaTodoSinPost_CuandoLaSedeExplicitaNoExiste()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, "FANTASMA", "CC-1111");

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.SedeNoExiste, "FANTASMA"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaTodoSinPost_CuandoLaSedeExplicitaEstaInactiva()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, "NORTE", "CC-1111");

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.SedeInactiva, "NORTE"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_TrataLaSedeEnBlancoComoAusente_CuandoSedeDeProgramacionVieneVacia()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, "   ", "CC-1111");

        entorno.PostDe("CC-1111")["sede"]!["id"]!.GetValue<string>().Should().Be("SUBA");
        JsonNode.Parse(resultado)!["programados"]!.AsArray().Should().ContainSingle();
    }

    // Franjas con sede prearmada: la cascada no necesita el maestro ni el default.
    [Fact]
    public async Task SolicitarProgramacionTurno_NoLeeElMaestroNiEnviaDefaultNiAvisa_CuandoTodasLasFranjasTraenSede()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja("SUBA"),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "NORTE")));

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        entorno.Sedes.Requests.Should().BeEmpty();
        entorno.PostDe("CC-1111")["sede"].Should().BeNull();
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Single()!.AsObject().ContainsKey("sede").Should().BeFalse();
        json.AsObject().ContainsKey("avisos").Should().BeFalse();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinPost_CuandoFallaLaLecturaDelMaestroDeSedes()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")),
            HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(entorno, null, "CC-1111");

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.RechazoDelDominio, "boom"));
        entorno.Posts.Should().BeEmpty();
    }

    // CA-6
    [Fact]
    public async Task SolicitarProgramacionTurno_EmiteElIndicadorSinSedeDeProgramacion_CuandoNoViajaSedeExplicita()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Directorio(("CC-1111", "AR01", "SUBA")));

        await Ejecutar(entorno, null, "CC-1111");

        var registro = entorno.Logger.Registros.Single(r => r.EventId.Name == "EjecucionSolicitudProgramacion");
        registro.Nivel.Should().Be(LogLevel.Information);
        registro.Propiedades.GetValueOrDefault("SedeDeProgramacion").Should().BeNull();
        registro.Propiedades["Programados"].Should().Be(1);
    }
}
