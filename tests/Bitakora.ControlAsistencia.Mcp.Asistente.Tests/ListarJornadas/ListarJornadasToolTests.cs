using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarJornadas;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarJornadas;

public class ListarJornadasToolTests
{
    private static async Task<(JsonNode Json, HandlerEnlatado Handler)> Ejecutar(string fixture, string? cursor = null)
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarJornadas", fixture));
        var tool = new ListarJornadasTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, cursor, TestContext.Current.CancellationToken);

        return (JsonNode.Parse(resultado)!, handler);
    }

    [Fact]
    public async Task ListarJornadas_RemodelaCadaJornadaAIdDescripcionYPredeterminada_CuandoElCatalogoResponde()
    {
        var (json, _) = await Ejecutar("listar-jornadas.json");

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.AsObject().ContainsKey("siguienteCursor").Should().BeFalse();
        json.AsObject().ContainsKey("nota").Should().BeFalse();

        var primera = json["jornadas"]![0]!.AsObject();
        primera["jornada_id"]!.GetValue<string>().Should().Be("01a08700-0000-7000-9000-000000000001");
        primera["descripcion"]!.GetValue<string>().Should().StartWith("42:00 semanales");
        primera["es_predeterminada"]!.GetValue<bool>().Should().BeTrue();
        json["jornadas"]![1]!["es_predeterminada"]!.GetValue<bool>().Should().BeFalse();
        primera.Count.Should().Be(3, "el elemento solo lleva id, descripcion y es_predeterminada");
    }

    [Fact]
    public async Task ListarJornadas_PideTakeMaximoMasUno_CuandoNoRecibeCursor()
    {
        var (_, handler) = await Ejecutar("listar-jornadas.json");

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Get);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/programacion/jornadas");
        handler.UltimaRequest.RequestUri.Query.Should().Be("?take=51");
    }

    [Fact]
    public async Task ListarJornadas_MuestraElTopeYReenviaElCursorConNota_CuandoElBackendDevuelveUnaPaginaDeMas()
    {
        var (json, _) = await Ejecutar("listar-jornadas-pagina-llena.json");

        json["mostrando"]!.GetValue<int>().Should().Be(ListarJornadasTool.MaximoJornadas);
        json["jornadas"]!.AsArray().Should().HaveCount(50);
        json["siguienteCursor"]!.GetValue<string>()
            .Should().Be("eyJIb3JhcyI6MjQwMCwiVG9wZSI6NDgwLCJJZCI6IngifQ");
        json["nota"]!.GetValue<string>().Should().Be(ListarJornadasTool.Mensajes.NotaMasJornadas);
    }

    [Fact]
    public async Task ListarJornadas_EnviaElCursorTalCual_CuandoRecibeCursor()
    {
        var (_, handler) = await Ejecutar("listar-jornadas.json", cursor: "abc_-DEF123");

        handler.UltimaRequest!.RequestUri!.Query.Should().Contain("cursor=abc_-DEF123").And.Contain("take=51");
    }
}
