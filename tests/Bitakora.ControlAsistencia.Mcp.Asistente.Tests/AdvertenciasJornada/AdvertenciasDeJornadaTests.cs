using System.Net;
using System.Reflection;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AdvertenciasJornada;

public class AdvertenciasDeJornadaTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string Advertencia =
        "El tope diario de 9:00 supera las 8:24 que la ley permite a la modalidad Ordinaria";

    private static async Task<JsonNode> Listar(string fixture)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("AdvertenciasJornada", fixture));
        var tool = new ListarJornadasTool(new ProgramacionApi(cliente));
        return JsonNode.Parse(await tool.Run(null!, null, TestContext.Current.CancellationToken))!;
    }

    private static async Task<JsonNode> AsignarModalidad(string fixtureGet)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Put, $"/api/programacion/jornadas/{JornadaId}/modalidad", HttpStatusCode.NoContent, "");
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("AdvertenciasJornada", fixtureGet));
        var tool = new AsignarModalidadAJornadaTool(new ProgramacionApi(cliente));
        return JsonNode.Parse(
            await tool.Run(null!, JornadaId, "Ordinaria", TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task ListarJornadas_IncluyeLasDescripcionesDeLasAdvertencias_CuandoLaJornadaLasTiene()
    {
        var json = await Listar("listar-jornadas-con-advertencias.json");

        var advertencias = json["jornadas"]![0]!["advertencias"]!.AsArray();
        advertencias.Select(a => a!.GetValue<string>()).Should().Equal(
            Advertencia, "Las horas semanales superan las que la ley permite");
    }

    [Fact]
    public async Task ListarJornadas_OmiteAdvertencias_CuandoVienenVaciasOAusentes()
    {
        var json = await Listar("listar-jornadas-con-advertencias.json");

        json["jornadas"]![1]!.AsObject().ContainsKey("advertencias").Should().BeFalse();
        json["jornadas"]![2]!.AsObject().ContainsKey("advertencias").Should().BeFalse();
    }

    [Fact]
    public async Task ListarJornadas_NoExponeTipoMagnitudNiValorDeLey_CuandoLaJornadaTieneAdvertencias()
    {
        var json = await Listar("listar-jornadas-con-advertencias.json");

        json["jornadas"]![0]!.ToJsonString().Should()
            .NotContain("TopeDiarioSuperaLey").And.NotContain("magnitud").And.NotContain("valorLey");
    }

    [Fact]
    public async Task AsignarModalidadAJornada_DevuelveLaAdvertenciaDelTopeDiario_CuandoLaModalidadNoCabe()
    {
        var json = await AsignarModalidad("jornada-con-advertencias.json");

        json["modalidad"]!.GetValue<string>().Should().Be("Ordinaria");
        json["advertencias"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal(Advertencia);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_OmiteAdvertencias_CuandoLaJornadaNoTieneNinguna()
    {
        var json = await AsignarModalidad("jornada-sin-advertencias.json");

        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json.AsObject().ContainsKey("advertencias").Should().BeFalse();
    }

    [Fact]
    public void JornadaDelCatalogo_DescribeConAdvertencias_CuandoElBcLasDevuelve()
    {
        var jornada = System.Text.Json.JsonSerializer.Deserialize<JornadaDelCatalogo>(
            Fixtures.Leer("AdvertenciasJornada", "jornada-con-advertencias.json"),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var json = JsonNode.Parse(RespuestaJson.Serializar(jornada.Describir()))!;

        json["advertencias"]![0]!.GetValue<string>().Should().Be(Advertencia);
    }

    [Fact]
    public void ListarJornadas_DescribeQueSinAdvertenciasNoEsConformeALaLey_EnLaDescripcionDeLaTool()
    {
        var run = typeof(ListarJornadasTool).GetMethod(nameof(ListarJornadasTool.Run))!;
        var descripcion = run.GetParameters()[0].GetCustomAttribute<McpToolTriggerAttribute>()!.Description;

        descripcion.Should().Contain("advertencias").And.Contain("sin advertencias contra la modalidad")
            .And.Contain("conforme a la ley");
    }

    [Fact]
    public void InstruccionesDelServidor_DicenQueSinAdvertenciasNoEsConformeALaLey_EnHostJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json")))
            dir = dir.Parent;
        dir.Should().NotBeNull();
        var host = File.ReadAllText(Path.Combine(dir!.FullName, "src", "Bitakora.ControlAsistencia.Mcp.Asistente", "host.json"));

        host.Should().Contain("sin advertencias contra la modalidad").And.Contain("conforme a la ley");
    }
}
