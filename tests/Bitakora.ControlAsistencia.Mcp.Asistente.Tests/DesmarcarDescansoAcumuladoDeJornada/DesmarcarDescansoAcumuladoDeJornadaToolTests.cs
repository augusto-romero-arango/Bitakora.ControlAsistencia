using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.DesmarcarDescansoAcumuladoDeJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.DesmarcarDescansoAcumuladoDeJornada;

public class DesmarcarDescansoAcumuladoDeJornadaToolTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private static readonly string RutaMarca = $"/api/programacion/jornadas/{JornadaId}/descanso-acumulado";

    private static (DesmarcarDescansoAcumuladoDeJornadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode status = HttpStatusCode.NoContent, string cuerpo = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Delete, RutaMarca, status, cuerpo);
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("CrearJornada", "jornada-con-modalidad.json"));
        return (new DesmarcarDescansoAcumuladoDeJornadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_RespondeFraseSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "no-es-guid", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(DesmarcarDescansoAcumuladoDeJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_EnviaLaMarcaYResponderDesdeElGet_CuandoElIdEsValido()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        handler.Requests.Single(r => r.Metodo == HttpMethod.Delete).Ruta.Should().Be(RutaMarca);
        handler.Requests.Single(r => r.Metodo == HttpMethod.Get).Ruta
            .Should().Be($"/api/programacion/jornadas/{JornadaId}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["descanso_acumulado"]!.GetValue<bool>().Should().Be(false);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe la Jornada";
        var (tool, handler) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(DesmarcarDescansoAcumuladoDeJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "La Jornada no tiene modalidad";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(DesmarcarDescansoAcumuladoDeJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
