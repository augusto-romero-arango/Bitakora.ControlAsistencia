using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;

public class AsignarJornadaToolTests
{
    private const string JornadaTexto = "01a08700-0000-7000-9000-000000000001";
    private static readonly Guid Jornada = Guid.Parse(JornadaTexto);
    private static readonly string RutaJornada = $"/api/programacion/jornadas/{JornadaTexto}";
    private const string MotivoDel409 = "La vinculacion del colaborador ya termino";

    private static string JornadaJson => Fixtures.Leer("CrearJornada", "jornada-obtenida.json");

    private static (AsignarJornadaTool Tool, HandlerPorRuta Programacion, ColaboradoresFalso Colaboradores)
        CrearEntorno(
            IReadOnlyList<object> directorio,
            HttpStatusCode statusJornada = HttpStatusCode.OK,
            string? cuerpoJornada = null,
            Func<string, HttpResponseMessage>? respuestaPut = null,
            TimeSpan? demoraPut = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaJornada, statusJornada, cuerpoJornada ?? JornadaJson);

        var colaboradores = new ColaboradoresFalso(directorio, respuestaPut, demoraPut);
        var apiColaboradores = new ColaboradoresApi(
            new HttpClient(colaboradores) { BaseAddress = new Uri("https://dominio.falso.local") });

        var tool = new AsignarJornadaTool(
            new ProgramacionApi(clienteProgramacion), apiColaboradores,
            new ResolutorCandidatosPorLista(apiColaboradores));
        return (tool, programacion, colaboradores);
    }

    private static Task<string> Ejecutar(AsignarJornadaTool tool, string jornadaId, string identificaciones) =>
        tool.Run(null!, jornadaId, identificaciones, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarJornada_RespondeFraseSinLlamar_CuandoElJornadaIdNoEsGuid()
    {
        var (tool, programacion, colaboradores) = CrearEntorno([]);

        var resultado = await Ejecutar(tool, "no-es-guid", "CC-1");

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        programacion.Requests.Should().BeEmpty();
        colaboradores.Consultas.Should().BeEmpty();
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornada_RespondeJornadaNoExiste_CuandoObtenerJornadaDevuelve404()
    {
        var (tool, _, colaboradores) = CrearEntorno([ColaboradoresFalso.Entrada(1)], HttpStatusCode.NotFound, "");

        var resultado = await Ejecutar(tool, JornadaTexto, "CC-1");

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaNoExiste, JornadaTexto));
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornada_RespondeJornadaDesactivada_CuandoLaJornadaNoEstaActiva()
    {
        var (tool, _, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Entrada(1)],
            cuerpoJornada: JornadaJson.Replace("\"activa\": true", "\"activa\": false"));

        var resultado = await Ejecutar(tool, JornadaTexto, "CC-1");

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaDesactivada, JornadaTexto));
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornada_AsignaSoloALosQueNoLaTienen_CuandoHayYaAsignadosFallidosYNoEncontrados()
    {
        var (tool, _, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Entrada(1), ColaboradoresFalso.Entrada(2, Jornada), ColaboradoresFalso.Entrada(3)],
            respuestaPut: id => id == "CC-3"
                ? ColaboradoresFalso.Json(HttpStatusCode.Conflict, MotivoDel409)
                : new HttpResponseMessage(HttpStatusCode.NoContent));

        var resultado = await Ejecutar(tool, JornadaTexto, "CC-1, CC-2, CC-3, CC-9");

        colaboradores.IdentificacionesConPut.Should().BeEquivalentTo(["CC-1", "CC-3"]);
        colaboradores.Puts.Should().OnlyContain(p => p.Cuerpo["jornadaId"]!.GetValue<string>() == JornadaTexto);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarJornadaTool.Mensajes.ResultadoJornadaAsignada);
        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaTexto);
        json["jornada"]!["descripcion"]!.GetValue<string>().Should().StartWith("42:00 semanales");
        json["asignados"]!.GetValue<int>().Should().Be(1);
        json["ya_la_tenian"]!.GetValue<int>().Should().Be(1);
        json["no_encontrados"]!.AsArray().Select(n => n!.GetValue<string>()).Should().BeEquivalentTo(["CC-9"]);
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["identificacion"]!.GetValue<string>().Should().Be("CC-3");
        fallido["motivo"]!.GetValue<string>().Should().Contain(MotivoDel409);
        json["nota"]!.GetValue<string>().Should().Be(AsignarJornadaTool.Mensajes.NotaVisibilidadEventual);
        json.AsObject().ContainsKey("selector").Should().BeFalse();
    }

    [Fact]
    public async Task AsignarJornada_OmiteFallidosYNoEncontrados_CuandoTodosSeAsignan()
    {
        var (tool, _, _) = CrearEntorno([ColaboradoresFalso.Entrada(1)]);

        var resultado = await Ejecutar(tool, JornadaTexto, "CC-1");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["asignados"]!.GetValue<int>().Should().Be(1);
        json["ya_la_tenian"]!.GetValue<int>().Should().Be(0);
        json.ContainsKey("fallidos").Should().BeFalse();
        json.ContainsKey("no_encontrados").Should().BeFalse();
    }

    [Fact]
    public async Task AsignarJornada_NuncaTieneMasDeOchoPutsEnVuelo_CuandoHayVeinteCandidatos()
    {
        var (tool, _, colaboradores) = CrearEntorno(
            [.. Enumerable.Range(1, 20).Select(n => ColaboradoresFalso.Entrada(n))],
            demoraPut: TimeSpan.FromMilliseconds(25));

        var resultado = await Ejecutar(tool, JornadaTexto, string.Join(",", Enumerable.Range(1, 20).Select(n => $"CC-{n}")));

        colaboradores.Puts.Should().HaveCount(20);
        colaboradores.MaximoEnVuelo.Should().BeLessThanOrEqualTo(8).And.BeGreaterThan(1);
        JsonNode.Parse(resultado)!["asignados"]!.GetValue<int>().Should().Be(20);
    }
}
