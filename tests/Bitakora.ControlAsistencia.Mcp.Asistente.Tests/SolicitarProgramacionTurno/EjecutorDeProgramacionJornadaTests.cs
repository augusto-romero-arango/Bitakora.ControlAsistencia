using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurno;

public class EjecutorDeProgramacionJornadaTests
{
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private static readonly Guid J1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly TurnoAProgramar Manana = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Manana", false);
    private static readonly SedeProgramada Sede = new("SUBA", "Suba", null);
    private static readonly FichaTurno FichaSinFranjas = new("x", "x", false, "", [], "", true);

    private static CandidatoProgramacion Candidato(string identificacion, Guid? jornadaId) =>
        new(identificacion, "C" + identificacion, "Colab " + identificacion,
            new DateOnly(2025, 1, 1), null, null, jornadaId);

    private static async Task<(ResultadoEjecucion Resultado, List<JsonNode> Posts)> Ejecutar(
        IReadOnlyList<CandidatoProgramacion> candidatos, Func<JsonNode, HttpResponseMessage>? responder = null)
    {
        var posts = new List<JsonNode>();
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Post, RutaSolicitudes, (_, cuerpo) =>
        {
            var json = JsonNode.Parse(cuerpo!)!;
            lock (posts)
                posts.Add(json);
            return responder?.Invoke(json)
                ?? new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") };
        });

        var (clienteSedes, _) = ClienteFalso.Con("[]");
        var plan = (await PlanDeSede.CrearAsync(
            new SedesApi(clienteSedes), Sede, FichaSinFranjas,
            new MotivosDeAviso("s", "i", "n"), CancellationToken.None)).Plan!;
        var resultado = await EjecutorDeProgramacion.EjecutarAsync(
            new ProgramacionApi(cliente), candidatos, AsignacionDeTurno.UnSoloTurno(Manana), plan,
            VentanaDeProgramacion.Crear(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3)),
            new ContadoresDeEjecucion(), CancellationToken.None);
        return (resultado, posts);
    }

    private static string Identificacion(JsonNode post) =>
        post["colaborador"]!["identificacion"]!.GetValue<string>();

    // Programar ya no estampa la Jornada (#1520): el candidato la conserva para asignar_jornada,
    // pero la solicitud no la envia.
    [Fact]
    public async Task EjecutarAsync_NoEnviaJornadaId_AunqueElCandidatoTengaJornada()
    {
        var (_, posts) = await Ejecutar([Candidato("CC-A", J1), Candidato("CC-B", null)]);

        posts.Should().HaveCount(2)
            .And.OnlyContain(p => !p.AsObject().ContainsKey("jornadaId"));
    }

    [Fact]
    public async Task EjecutarAsync_ReportaFallidoSoloAlCandidatoRechazado_CuandoElServidorRespondeNotFound()
    {
        var (resultado, _) = await Ejecutar(
            [Candidato("CC-A", J1), Candidato("CC-B", null)],
            p => Identificacion(p) == "CC-A"
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("turno no encontrado", Encoding.UTF8) }
                : new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") });

        var fallido = resultado.Fallidos.Should().ContainSingle().Subject;
        fallido.Identificacion.Should().Be("CC-A");
        fallido.Motivo.Should().Be("turno no encontrado");
        resultado.Programados.Should().ContainSingle().Which.Identificacion.Should().Be("CC-B");
    }
}
