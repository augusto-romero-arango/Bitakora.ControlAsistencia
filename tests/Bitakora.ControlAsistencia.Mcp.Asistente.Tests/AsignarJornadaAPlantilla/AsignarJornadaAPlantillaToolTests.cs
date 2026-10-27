using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornadaAPlantilla;

public class AsignarJornadaAPlantillaToolTests
{
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string DescripcionJornada =
        "42:00 semanales, tope diario 8:00, minimo diario 0:00, 1 dia de descanso por semana";

    private static string JornadaJson => Fixtures.Leer("AsignarJornadaAPlantilla", "obtener-jornada.json");

    private sealed record Fakes(AsignarJornadaAPlantillaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusJornada = HttpStatusCode.OK,
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, RutaDetalle(PlantillaId), statusDetalle,
            statusDetalle == HttpStatusCode.OK
                ? Plantilla(PlantillaId, "Ciclo Cocina", 7, true, [Dia(1, Turno("Cocina Manana"))])
                : "");
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", statusJornada, JornadaJson);
        handler.Responde(HttpMethod.Put, $"{RutaCatalogo}/{PlantillaId}/jornada", statusPut, cuerpoPut);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new AsignarJornadaAPlantillaTool(api, new ResolutorPlantillaTurnosPorId(api)), handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string plantillaId = PlantillaId, string jornadaId = JornadaId) =>
        fakes.Tool.Run(null!, plantillaId, jornadaId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarJornadaAPlantilla_LeeLaJornadaYHacePutConJornadaId_CuandoTodoExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaDetalle(PlantillaId)),
            (HttpMethod.Get, $"{RutaJornadas}/{JornadaId}"),
            (HttpMethod.Put, $"{RutaCatalogo}/{PlantillaId}/jornada"));
        JsonNode.Parse(fakes.Handler.Requests[2].Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarJornadaAPlantillaTool.Mensajes.ResultadoJornadaAsignada);
        json["plantilla"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["jornada"]!["descripcion"]!.GetValue<string>().Should().Be(DescripcionJornada);
        json["nota"]!.GetValue<string>().Should().Be(AsignarJornadaAPlantillaTool.Mensajes.NotaAdvertenciasEventuales);
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_RechazaSinLlamadasHttp_CuandoJornadaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, jornadaId: "no-es-guid");

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Ciclo Cocina");

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_RespondePlantillaNoExisteSinPut_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.PlantillaNoExiste, PlantillaId));
        resultado.Should().Contain("buscar_plantillas");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_RespondeJornadaNoExisteSinPut_Cuando404DeLaJornada()
    {
        var fakes = CrearTool(statusJornada: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(AsignarJornadaAPlantillaTool.Mensajes.JornadaNoExiste);
        resultado.Should().Contain("listar_jornadas");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_TraduceElRechazoDelDominio_Cuando409PorPlantillaRetirada()
    {
        const string cuerpo = "La plantilla esta retirada";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_TraduceElRechazoDelDominio_Cuando409PorJornadaDesactivada()
    {
        const string cuerpo = "La Jornada esta desactivada";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarJornadaAPlantilla_RespondeElRechazoDelDominioSinPut_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
