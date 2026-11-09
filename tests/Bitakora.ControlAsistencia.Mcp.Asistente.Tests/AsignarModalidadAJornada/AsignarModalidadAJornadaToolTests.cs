using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarModalidadAJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarModalidadAJornada;

public class AsignarModalidadAJornadaToolTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private static readonly string RutaModalidad = $"/api/programacion/jornadas/{JornadaId}/modalidad";

    private static (AsignarModalidadAJornadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusPut = HttpStatusCode.NoContent, string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Put, RutaModalidad, statusPut, cuerpoPut);
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("CrearJornada", "jornada-con-modalidad.json"));
        return (new AsignarModalidadAJornadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_RespondeFraseSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "no-es-guid", "Ordinaria", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarModalidadAJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_RespondeFraseSinLlamarAlServidor_CuandoLaModalidadNoSeReconoce()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, JornadaId, "Rara", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarModalidadAJornadaTool.Mensajes.ModalidadInvalida, "Rara"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarModalidadAJornada_EnviaElPutYResponderDesdeElGet_CuandoLosDatosSonValidos()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, JornadaId, "Ordinaria", TestContext.Current.CancellationToken);

        var put = handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        put.Ruta.Should().Be(RutaModalidad);
        JsonNode.Parse(put.Cuerpo!)!["modalidad"]!.GetValue<string>().Should().Be("Ordinaria");
        handler.Requests.Single(r => r.Metodo == HttpMethod.Get).Ruta
            .Should().Be($"/api/programacion/jornadas/{JornadaId}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["modalidad"]!.GetValue<string>().Should().Be("Ordinaria");
    }

    [Fact]
    public async Task AsignarModalidadAJornada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe la Jornada";
        var (tool, handler) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, "Flexible", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarModalidadAJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe otra Jornada con esos limites y esa modalidad";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, "TurnosSucesivos", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarModalidadAJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
