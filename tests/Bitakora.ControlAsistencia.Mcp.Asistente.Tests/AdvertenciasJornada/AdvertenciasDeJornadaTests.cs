using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;
using Bitakora.ControlAsistencia.Mcp.Asistente.QuitarModalidadDeJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AdvertenciasJornada;

public class AdvertenciasDeJornadaTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string RutaJornada = $"/api/programacion/jornadas/{JornadaId}";
    private const string AdvertenciaTope =
        "El tope diario (9:00) supera en 0:36 el de la modalidad Ordinaria (8:24)";
    private const string AdvertenciaSemanal =
        "Las horas semanales (44:00) superan en 2:00 el techo de la modalidad Ordinaria (42:00)";

    private static async Task<JsonNode> Listar(string fixture)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("AdvertenciasJornada", fixture));
        var tool = new ListarJornadasTool(new ProgramacionApi(cliente));
        return JsonNode.Parse(await tool.Run(null!, null, TestContext.Current.CancellationToken))!;
    }

    private static ProgramacionApi ApiQueModificaYLee(HttpMethod metodo, string fixtureGet)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(metodo, $"{RutaJornada}/modalidad", HttpStatusCode.NoContent, "");
        handler.Responde(
            HttpMethod.Get, RutaJornada, HttpStatusCode.OK, Fixtures.Leer("AdvertenciasJornada", fixtureGet));
        return new ProgramacionApi(cliente);
    }

    private static async Task<JsonNode> AsignarOrdinaria(string fixtureGet)
    {
        var tool = new AsignarModalidadAJornadaTool(ApiQueModificaYLee(HttpMethod.Put, fixtureGet));
        return JsonNode.Parse(
            await tool.Run(null!, JornadaId, "Ordinaria", TestContext.Current.CancellationToken))!;
    }

    [Fact]
    public async Task ListarJornadas_IncluyeLasDescripcionesDeLasAdvertencias_CuandoLaJornadaLasTiene()
    {
        var json = await Listar("listar-jornadas-con-advertencias.json");

        var advertencias = json["jornadas"]![0]!["advertencias"]!.AsArray();
        advertencias.Select(a => a!.GetValue<string>()).Should().Equal(AdvertenciaTope, AdvertenciaSemanal);
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
            .NotContain("SuperaTopeDiarioDeLaModalidad").And.NotContain("magnitud").And.NotContain("valorDeLey");
    }

    [Fact]
    public async Task AsignarModalidadAJornada_DevuelveLaAdvertenciaDelTopeDiario_CuandoLaModalidadNoCabe()
    {
        var json = await AsignarOrdinaria("jornada-con-advertencias.json");

        json["modalidad"]!.GetValue<string>().Should().Be("Ordinaria");
        json["advertencias"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal(AdvertenciaTope);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_OmiteAdvertencias_CuandoLaJornadaNoTieneNinguna()
    {
        var json = await AsignarOrdinaria("jornada-sin-advertencias.json");

        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json.AsObject().ContainsKey("advertencias").Should().BeFalse();
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_DevuelveLaJornadaSinAdvertencias_CuandoQuedaPactadaLibre()
    {
        var tool = new QuitarModalidadDeJornadaTool(ApiQueModificaYLee(HttpMethod.Delete, "jornada-sin-advertencias.json"));

        var json = JsonNode.Parse(await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken))!;

        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json.AsObject().ContainsKey("advertencias").Should().BeFalse();
    }

    [Fact]
    public void JornadaDelCatalogo_DescribeConAdvertencias_CuandoElBcLasDevuelve()
    {
        var jornada = JsonSerializer.Deserialize<JornadaDelCatalogo>(
            Fixtures.Leer("AdvertenciasJornada", "jornada-con-advertencias.json"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var json = JsonNode.Parse(RespuestaJson.Serializar(jornada.Describir()))!;

        json["advertencias"]![0]!.GetValue<string>().Should().Be(AdvertenciaTope);
    }
}
