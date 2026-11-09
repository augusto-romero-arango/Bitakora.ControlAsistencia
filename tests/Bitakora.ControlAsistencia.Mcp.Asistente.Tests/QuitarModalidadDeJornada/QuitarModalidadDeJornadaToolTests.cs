using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.QuitarModalidadDeJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.QuitarModalidadDeJornada;

public class QuitarModalidadDeJornadaToolTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private static readonly string RutaModalidad = $"/api/programacion/jornadas/{JornadaId}/modalidad";

    private static (QuitarModalidadDeJornadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusDelete = HttpStatusCode.NoContent, string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Delete, RutaModalidad, statusDelete, cuerpoDelete);
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("CrearJornada", "jornada-obtenida.json"));
        return (new QuitarModalidadDeJornadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_RespondeFraseSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "no-es-guid", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarModalidadDeJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_EnviaElDeleteYResponderDesdeElGetSinModalidad_CuandoElIdEsValido()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        handler.Requests.Single(r => r.Metodo == HttpMethod.Delete).Ruta.Should().Be(RutaModalidad);
        handler.Requests.Single(r => r.Metodo == HttpMethod.Get).Ruta
            .Should().Be($"/api/programacion/jornadas/{JornadaId}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json.AsObject().ContainsKey("modalidad").Should().BeFalse("sin modalidad el campo se omite (null)");
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe la Jornada";
        var (tool, handler) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarModalidadDeJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe otra Jornada con esos limites sin modalidad";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(null!, JornadaId, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarModalidadDeJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
