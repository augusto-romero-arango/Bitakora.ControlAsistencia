using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurnoPorGrupo;

public class CascadaDeSedeGrupoTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private sealed record Entorno(
        SolicitarProgramacionTurnoPorGrupoTool Tool, HandlerPorRuta Programacion, HandlerFuncional Sedes)
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
        string turnosJson, string fichasJson, HttpStatusCode statusMaestro = HttpStatusCode.OK)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, turnosJson);
        programacion.Responde(HttpMethod.Post, RutaSolicitudes, HttpStatusCode.Created, "");
        var (clienteSedes, sedes) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r, statusMaestro));
        var (clienteColaboradores, _) = ClienteFalso.Con(fichasJson);

        var tool = new SolicitarProgramacionTurnoPorGrupoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));
        return new Entorno(tool, programacion, sedes);
    }

    private static Task<string> Ejecutar(Entorno entorno, string? sedeDeProgramacion, string? sede = null) =>
        entorno.Tool.Run(
            context: null!,
            desde: "2026-09-01",
            hasta: "2026-09-03",
            turno: "Cocina Manana",
            sedeDeProgramacion: sedeDeProgramacion,
            sede: sede,
            etiquetas: "area:cocina",
            ct: TestContext.Current.CancellationToken);

    // CA-2 / CA-4
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EnviaLaSedeDeCadaColaboradorResuelta_CuandoNoViajaSedeExplicita()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Fichas(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, null);

        var sede = entorno.PostDe("CC-1111")["sede"]!;
        sede["id"]!.GetValue<string>().Should().Be("SUBA");
        sede["nombre"]!.GetValue<string>().Should().Be("Sede Suba");
        sede["centroDeCostos"]!.GetValue<string>().Should().Be("CC-100");
        var json = JsonNode.Parse(resultado)!;
        json["sede"].Should().BeNull();
        json["programados"]!.AsArray().Single()!["sede"]!.GetValue<string>().Should().Be("SUBA");
        json.AsObject().ContainsKey("avisos").Should().BeFalse();
        entorno.LecturasDelMaestro.Should().ContainSingle();
        entorno.LecturasDelMaestro.Single().RequestUri!.Query.Should().NotContain("activa");
    }

    // CA-3 / CA-4
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_ProgramaSinSedeYAvisaPorCadaCausa_CuandoLaSedeDelColaboradorNoEstaDisponible()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Fichas(
                ("CC-1111", "AR01", null), ("CC-2222", "BD01", "NORTE"),
                ("CC-3333", "CL01", "FANTASMA"), ("CC-4444", "DE01", "SUBA")));

        var resultado = await Ejecutar(entorno, null);

        entorno.Posts.Should().HaveCount(4);
        foreach (var identificacion in new[] { "CC-1111", "CC-2222", "CC-3333" })
            entorno.PostDe(identificacion)["sede"].Should().BeNull();
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(4);
        json.AsObject().ContainsKey("fallidos").Should().BeFalse();

        var motivos = json["avisos"]!.AsArray()
            .ToDictionary(a => a!["identificacion"]!.GetValue<string>(), a => a!["motivo"]!.GetValue<string>());
        motivos.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["CC-1111"] = SolicitarProgramacionTurnoPorGrupoTool.Mensajes.AvisoSinSede,
            ["CC-2222"] = string.Format(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.AvisoSedeInactiva, "NORTE"),
            ["CC-3333"] = string.Format(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.AvisoSedeNoExiste, "FANTASMA")
        });
    }

    // CA-1
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EnviaLaSedeExplicitaATodos_CuandoViajaSedeDeProgramacion()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Fichas(("CC-1111", "AR01", null), ("CC-2222", "BD01", "NORTE")));

        var resultado = await Ejecutar(entorno, "SUBA");

        entorno.Posts.Should().HaveCount(2)
            .And.OnlyContain(p => p["sede"]!["id"]!.GetValue<string>() == "SUBA");
        var json = JsonNode.Parse(resultado)!;
        json["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        json["programados"]!.AsArray().Should().AllSatisfy(
            p => p!.AsObject().ContainsKey("sede").Should().BeFalse());
        json.AsObject().ContainsKey("avisos").Should().BeFalse();
        entorno.LecturasDelMaestro.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaTodoSinPost_CuandoLaSedeExplicitaEstaInactiva()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Fichas(("CC-1111", "AR01", "SUBA")));

        var resultado = await Ejecutar(entorno, "NORTE");

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SedeInactiva, "NORTE"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoLeeElMaestroNiAvisa_CuandoTodasLasFranjasTraenSede()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja("SUBA"),
            EntornoDeSedes.Fichas(("CC-1111", "AR01", null)));

        var resultado = await Ejecutar(entorno, null);

        entorno.Sedes.Requests.Should().BeEmpty();
        entorno.PostDe("CC-1111")["sede"].Should().BeNull();
        JsonNode.Parse(resultado)!.AsObject().ContainsKey("avisos").Should().BeFalse();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinPost_CuandoFallaLaLecturaDelMaestroDeSedes()
    {
        var entorno = Crear(
            EntornoDeSedes.TurnosConUnaFranja(null),
            EntornoDeSedes.Fichas(("CC-1111", "AR01", "SUBA")),
            HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(entorno, null);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.RechazoDelDominio, "boom"));
        entorno.Posts.Should().BeEmpty();
    }
}
