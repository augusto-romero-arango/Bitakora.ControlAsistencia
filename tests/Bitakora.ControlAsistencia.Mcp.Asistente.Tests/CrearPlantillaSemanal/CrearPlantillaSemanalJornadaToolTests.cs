using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearPlantillaSemanal;

// CA-1 de #892: crear_plantilla_semanal con jornada_id opcional.
public class CrearPlantillaSemanalJornadaToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string DescripcionJornada =
        "42:00 semanales, tope diario 8:00, minimo diario 0:00, 1 dia de descanso por semana";
    private const string UnDiaConTurnoJson = """[{"semana":1,"dia":"lunes","turno":"Cocina Manana"}]""";
    private const string UnDiaConFranjaJson = """[{"semana":1,"dia":"lunes","franja":"07:00-17:00"}]""";

    private sealed record Fakes(CrearPlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(HttpStatusCode statusJornada = HttpStatusCode.OK)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", statusJornada,
            Fixtures.Leer("AsignarJornadaAPlantillaSemanal", "obtener-jornada.json"));
        handler.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK,
            Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json"));
        handler.Responde(HttpMethod.Post, RutaPlantillas, HttpStatusCode.Created);
        handler.RespondeConPrefijo(HttpMethod.Put, $"{RutaPlantillas}/", HttpStatusCode.NoContent);
        handler.Responde(HttpMethod.Post, RutaTurnos, HttpStatusCode.Created);
        handler.RespondeConPrefijo(HttpMethod.Post, $"{RutaTurnos}/", HttpStatusCode.NoContent);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new CrearPlantillaSemanalTool(api, new ResolutorTurnoPorNombre(api)), handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string? jornadaId, string dias = UnDiaConTurnoJson) =>
        fakes.Tool.Run(null!, "Plantilla Jornada", 1, dias, jornadaId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task CrearPlantillaSemanal_LeeLaJornadaAntesDeTurnosYPostConJornadaId_CuandoSeIndicaJornadaId()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, JornadaId);

        var rutas = fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).ToList();
        rutas[0].Should().Be((HttpMethod.Get, $"{RutaJornadas}/{JornadaId}"));
        rutas[1].Should().Be((HttpMethod.Get, RutaTurnos));
        rutas[2].Should().Be((HttpMethod.Post, RutaPlantillas));

        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        JsonNode.Parse(post.Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);

        var json = JsonNode.Parse(resultado)!;
        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["jornada"]!["descripcion"]!.GetValue<string>().Should().Be(DescripcionJornada);
    }

    [Fact]
    public async Task CrearPlantillaSemanal_RechazaSinLlamadasHttp_CuandoJornadaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, "no-es-guid");

        resultado.Should().Be(string.Format(CrearPlantillaSemanalTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearPlantillaSemanal_NoCreaTurnosNiPlantilla_Cuando404DeLaJornada()
    {
        var fakes = CrearTool(HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes, JornadaId, UnDiaConFranjaJson);

        resultado.Should().Be(CrearPlantillaSemanalTool.Mensajes.JornadaNoExiste);
        fakes.Handler.Requests.Should().OnlyContain(r => r.Metodo == HttpMethod.Get && r.Ruta.StartsWith(RutaJornadas));
    }

    [Fact]
    public async Task CrearPlantillaSemanal_NoEnviaJornadaIdNiLaEcoa_CuandoNoSeIndicaJornadaId()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, null);

        fakes.Handler.Requests.Should().NotContain(r => r.Ruta.StartsWith(RutaJornadas));
        var post = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaPlantillas);
        var cuerpo = JsonNode.Parse(post.Cuerpo!)!.AsObject();
        (!cuerpo.ContainsKey("jornadaId") || cuerpo["jornadaId"] is null).Should().BeTrue();
        JsonNode.Parse(resultado)!.AsObject().ContainsKey("jornada").Should().BeFalse();
    }
}
