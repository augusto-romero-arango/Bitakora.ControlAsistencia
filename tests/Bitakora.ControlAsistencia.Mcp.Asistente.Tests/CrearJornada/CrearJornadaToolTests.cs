using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearJornada;

public class CrearJornadaToolTests
{
    private const string RutaJornadas = "/api/programacion/jornadas";

    private static (CrearJornadaTool Tool, HandlerPorRuta Handler) CrearTool(
        HttpStatusCode statusPost = HttpStatusCode.Created, string cuerpoPost = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Post, RutaJornadas, statusPost, cuerpoPost);
        handler.RespondeConPrefijo(
            HttpMethod.Get, RutaJornadas + "/", HttpStatusCode.OK, Fixtures.Leer("CrearJornada", "jornada-obtenida.json"));
        return (new CrearJornadaTool(new ProgramacionApi(cliente)), handler);
    }

    [Fact]
    public async Task CrearJornada_EnviaElPostConLosCuatroLimites_YResponderDesdeElGet_CuandoLosDatosSonValidos()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "42:00", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        var post = handler.Requests.First(r => r.Metodo == HttpMethod.Post);
        post.Ruta.Should().Be(RutaJornadas);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        Guid.TryParse(body["jornadaId"]!.GetValue<string>(), out var id).Should().BeTrue();
        body["horasSemanales"]!["horas"]!.GetValue<int>().Should().Be(42);
        body["horasSemanales"]!["minutos"]!.GetValue<int>().Should().Be(0);
        body["topeDiario"]!["horas"]!.GetValue<int>().Should().Be(8);
        body["minimoDiario"]!["horas"]!.GetValue<int>().Should().Be(0);
        body["minimoDiario"]!["minutos"]!.GetValue<int>().Should().Be(0);
        body["diasDescansoPorSemana"]!.GetValue<int>().Should().Be(1);

        var get = handler.Requests.Single(r => r.Metodo == HttpMethod.Get);
        get.Ruta.Should().Be($"{RutaJornadas}/{id}");

        var json = JsonNode.Parse(resultado)!;
        json["jornada_id"]!.GetValue<string>().Should().Be("01a08700-0000-7000-9000-000000000001");
        json["descripcion"]!.GetValue<string>().Should().StartWith("42:00 semanales");
        json["es_predeterminada"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task CrearJornada_RespondeFraseSinLlamarAlServidor_CuandoUnaDuracionEsInvalida()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "42:00", "8h", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.DuracionInvalida, "tope_diario", "8h"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearJornada_RespondeFraseSinLlamarAlServidor_CuandoLosMinutosSuperan59()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "42:00", "8:00", "8:75", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.DuracionInvalida, "minimo_diario", "8:75"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearJornada_RespondeFraseSinLlamarAlServidor_CuandoLasHorasSemanalesSonInvalidas()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "abc", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.DuracionInvalida, "horas_semanales", "abc"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearJornada_RespondeFraseSinLlamarAlServidor_CuandoLosDiasDeDescansoSuperan6()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "42:00", "8:00", "0:00", 7, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.DiasDescansoInvalido, 7));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearJornada_RespondeFraseSinLlamarAlServidor_CuandoLosDiasDeDescansoSonNegativos()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "42:00", "8:00", "0:00", -1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.DiasDescansoInvalido, -1));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearJornada_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe la Jornada 01a08700-0000-7000-9000-000000000009 con esos limites";
        var (tool, handler) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(null!, "42:00", "8:00", "0:00", 1, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
        handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get);
    }
}
