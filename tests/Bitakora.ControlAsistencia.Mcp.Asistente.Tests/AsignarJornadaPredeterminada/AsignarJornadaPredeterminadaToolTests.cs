using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPredeterminada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornadaPredeterminada;

public class AsignarJornadaPredeterminadaToolTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string RutaPreferencia = "/api/programacion/preferencias/jornada-predeterminada";

    private static (AsignarJornadaPredeterminadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusPut = HttpStatusCode.NoContent, string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Put, RutaPreferencia, statusPut, cuerpoPut);
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("CrearJornada", "jornada-predeterminada.json"));
        return (new AsignarJornadaPredeterminadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_RespondeFraseSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "no-es-guid", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarJornadaPredeterminadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_EnviaElPutConElId_YResponderDesdeElGet_CuandoLaJornadaExiste()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        var put = handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        put.Ruta.Should().Be(RutaPreferencia);
        JsonNode.Parse(put.Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);

        handler.Requests.Single(r => r.Metodo == HttpMethod.Get).Ruta
            .Should().Be($"/api/programacion/jornadas/{JornadaId}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["descripcion"]!.GetValue<string>().Should().StartWith("42:00 semanales");
        json["es_predeterminada"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task AsignarJornadaPredeterminada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe la Jornada";
        var (tool, handler) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarJornadaPredeterminadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }
}
