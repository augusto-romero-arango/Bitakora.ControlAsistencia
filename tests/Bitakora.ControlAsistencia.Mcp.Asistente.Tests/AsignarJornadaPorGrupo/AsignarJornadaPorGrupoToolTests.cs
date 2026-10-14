using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornadaPorGrupo;

public class AsignarJornadaPorGrupoToolTests
{
    private const string JornadaTexto = "01a08700-0000-7000-9000-000000000001";
    private static readonly Guid Jornada = Guid.Parse(JornadaTexto);
    private static readonly string RutaJornada = $"/api/programacion/jornadas/{JornadaTexto}";
    private const string MotivoDel409 = "La vinculacion del colaborador ya termino";

    private static string JornadaJson => Fixtures.Leer("CrearJornada", "jornada-obtenida.json");

    private static (AsignarJornadaPorGrupoTool Tool, HandlerPorRuta Programacion, ColaboradoresFalso Colaboradores)
        CrearEntorno(
            IReadOnlyList<object> fichas,
            HttpStatusCode statusJornada = HttpStatusCode.OK,
            string? cuerpoJornada = null,
            Func<string, HttpResponseMessage>? respuestaPut = null,
            TimeProvider? reloj = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaJornada, statusJornada, cuerpoJornada ?? JornadaJson);

        var (clienteSedes, _) = ClienteFalso.ConFuncion(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/SUBA", StringComparison.Ordinal)
                ? ColaboradoresFalso.Json(HttpStatusCode.OK, Fixtures.Leer("SolicitarProgramacionTurno", "sede.json"))
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var colaboradores = new ColaboradoresFalso(fichas, respuestaPut);
        var apiColaboradores = new ColaboradoresApi(
            new HttpClient(colaboradores) { BaseAddress = new Uri("https://dominio.falso.local") });

        var tool = new AsignarJornadaPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), apiColaboradores,
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorGrupo(apiColaboradores),
            reloj);
        return (tool, programacion, colaboradores);
    }

    private static Task<string> Ejecutar(
        AsignarJornadaPorGrupoTool tool, string jornadaId = JornadaTexto, string? sede = "SUBA", string? etiquetas = null) =>
        tool.Run(null!, jornadaId, sede, etiquetas, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarJornadaPorGrupo_RespondeFraseSinLlamar_CuandoElJornadaIdNoEsGuid()
    {
        var (tool, programacion, colaboradores) = CrearEntorno([]);

        var resultado = await Ejecutar(tool, "no-es-guid");

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        programacion.Requests.Should().BeEmpty();
        colaboradores.Consultas.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_RespondeJornadaNoExiste_CuandoObtenerJornadaDevuelve404()
    {
        var (tool, _, colaboradores) = CrearEntorno([ColaboradoresFalso.Ficha(1)], HttpStatusCode.NotFound, "");

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaNoExiste, JornadaTexto));
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_RespondeJornadaDesactivada_CuandoLaJornadaNoEstaActiva()
    {
        var (tool, _, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Ficha(1)],
            cuerpoJornada: JornadaJson.Replace("\"activa\": true", "\"activa\": false"));

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(AsignarJornadaTool.Mensajes.JornadaDesactivada, JornadaTexto));
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_RespondeLaFraseDelSelector_CuandoNoSeIndicaSedeNiEtiquetas()
    {
        var (tool, programacion, colaboradores) = CrearEntorno([ColaboradoresFalso.Ficha(1)]);

        var resultado = await Ejecutar(tool, sede: null, etiquetas: null);

        resultado.Should().Be(AsignarJornadaPorGrupoTool.Mensajes.SelectorObligatorio);
        colaboradores.Consultas.Should().BeEmpty();
        colaboradores.Puts.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_ResuelveLasFichasConHoyEnBogotaYElCodigoCanonicoDeLaSede_CuandoLaSedeEsValida()
    {
        var reloj = new RelojFalso(new DateTimeOffset(2026, 10, 14, 3, 0, 0, TimeSpan.Zero));
        var (tool, _, colaboradores) = CrearEntorno([ColaboradoresFalso.Ficha(1)], reloj: reloj);

        await Ejecutar(tool, sede: "SUBA");

        var consulta = colaboradores.Consultas.Single();
        consulta.Ruta.Should().Be("/api/colaboradores/fichas");
        consulta.Cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-13");
        consulta.Cuerpo["codigoSede"]!.GetValue<string>().Should().Be("SUBA");
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_RepartesAsignadosYaLaTenianYFallidos_CuandoElGrupoTieneVarios()
    {
        var (tool, _, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Ficha(1), ColaboradoresFalso.Ficha(2, Jornada), ColaboradoresFalso.Ficha(3)],
            respuestaPut: id => id == "CC-3"
                ? ColaboradoresFalso.Json(HttpStatusCode.Conflict, MotivoDel409)
                : new HttpResponseMessage(HttpStatusCode.NoContent));

        var resultado = await Ejecutar(tool, sede: "SUBA");

        colaboradores.IdentificacionesConPut.Should().BeEquivalentTo(["CC-1", "CC-3"]);
        var json = JsonNode.Parse(resultado)!.AsObject();
        json["asignados"]!.GetValue<int>().Should().Be(1);
        json["ya_la_tenian"]!.GetValue<int>().Should().Be(1);
        json["selector"]!.GetValue<string>().Should().Be("sede:SUBA");
        json.ContainsKey("no_encontrados").Should().BeFalse();
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["identificacion"]!.GetValue<string>().Should().Be("CC-3");
        fallido["motivo"]!.GetValue<string>().Should().Contain(MotivoDel409);
        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaTexto);
    }

    [Fact]
    public async Task AsignarJornadaPorGrupo_RespondeFraseConElCriterio_CuandoElGrupoNoTieneColaboradores()
    {
        var (tool, _, colaboradores) = CrearEntorno([]);

        var resultado = await Ejecutar(tool, sede: "SUBA");

        resultado.Should().Be(string.Format(AsignarJornadaPorGrupoTool.Mensajes.GrupoSinColaboradores, "sede:SUBA"));
        colaboradores.Puts.Should().BeEmpty();
    }
}
