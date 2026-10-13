using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ModificarLimitesJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ModificarLimitesJornada;

public class ModificarLimitesJornadaToolTests
{
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private static readonly string RutaLimites = $"/api/programacion/jornadas/{JornadaId}/limites";

    private static (ModificarLimitesJornadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusPut = HttpStatusCode.NoContent, string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Put, RutaLimites, statusPut, cuerpoPut);
        handler.Responde(
            HttpMethod.Get, $"/api/programacion/jornadas/{JornadaId}", HttpStatusCode.OK,
            Fixtures.Leer("CrearJornada", "jornada-obtenida.json"));
        return (new ModificarLimitesJornadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task ModificarLimitesJornada_RespondeFraseSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(
            null!, "no-es-guid", "42:00", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ModificarLimitesJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarLimitesJornada_RespondeFraseSinLlamarAlServidor_CuandoUnaDuracionEsInvalida()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(
            null!, JornadaId, "42:00", "8h", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ModificarLimitesJornadaTool.Mensajes.DuracionInvalida, "tope_diario", "8h"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarLimitesJornada_RespondeFraseSinLlamarAlServidor_CuandoLosDiasDeDescansoSuperan6()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(
            null!, JornadaId, "42:00", "8:00", "0:00", 9, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ModificarLimitesJornadaTool.Mensajes.DiasDescansoInvalido, 9));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ModificarLimitesJornada_EnviaElPutConLosCuatroLimites_YResponderDesdeElGet_CuandoLosDatosSonValidos()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(
            null!, JornadaId, "42:30", "8:00", "1:15", 2, TestContext.Current.CancellationToken);

        var put = handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        put.Ruta.Should().Be(RutaLimites);
        var body = JsonNode.Parse(put.Cuerpo!)!;
        body["horasSemanales"]!["horas"]!.GetValue<int>().Should().Be(42);
        body["horasSemanales"]!["minutos"]!.GetValue<int>().Should().Be(30);
        body["topeDiario"]!["horas"]!.GetValue<int>().Should().Be(8);
        body["minimoDiario"]!["horas"]!.GetValue<int>().Should().Be(1);
        body["minimoDiario"]!["minutos"]!.GetValue<int>().Should().Be(15);
        body["diasDescansoPorSemana"]!.GetValue<int>().Should().Be(2);

        handler.Requests.Single(r => r.Metodo == HttpMethod.Get).Ruta
            .Should().Be($"/api/programacion/jornadas/{JornadaId}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["descripcion"]!.GetValue<string>().Should().StartWith("42:00 semanales");
        json["es_predeterminada"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ModificarLimitesJornada_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "No existe la Jornada";
        var (tool, handler) = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await tool.Run(
            null!, JornadaId, "42:00", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ModificarLimitesJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }

    [Fact]
    public async Task ModificarLimitesJornada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe otra Jornada con esos limites";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(
            null!, JornadaId, "42:00", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ModificarLimitesJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
