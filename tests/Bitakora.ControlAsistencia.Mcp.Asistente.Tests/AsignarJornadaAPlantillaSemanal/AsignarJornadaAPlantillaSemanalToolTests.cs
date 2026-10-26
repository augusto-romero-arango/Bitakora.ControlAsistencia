using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarJornadaAPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornadaAPlantillaSemanal;

public class AsignarJornadaAPlantillaSemanalToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaJornadas = "/api/programacion/jornadas";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";
    private const string JornadaId = "01a08700-0000-7000-9000-000000000001";
    private const string DescripcionJornada =
        "42:00 semanales, tope diario 8:00, minimo diario 0:00, 1 dia de descanso por semana";

    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");

    private static string JornadaJson => Fixtures.Leer("AsignarJornadaAPlantillaSemanal", "obtener-jornada.json");

    private sealed record Fakes(AsignarJornadaAPlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusJornada = HttpStatusCode.OK,
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        if (statusDetalle == HttpStatusCode.OK)
            handler.RespondeDetalleDePlantilla(PlantillasJson, PlantillaIdCocina);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina), statusDetalle, "");
        handler.Responde(HttpMethod.Get, $"{RutaJornadas}/{JornadaId}", statusJornada, JornadaJson);
        handler.Responde(HttpMethod.Put, $"{RutaPlantillas}/{PlantillaIdCocina}/jornada", statusPut, cuerpoPut);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new AsignarJornadaAPlantillaSemanalTool(api, new ResolutorPlantillaPorId(api)), handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string plantillaId = PlantillaIdCocina, string jornadaId = JornadaId) =>
        fakes.Tool.Run(null!, plantillaId, jornadaId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_LeeLaJornadaYHacePutConJornadaId_CuandoTodoExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina)),
            (HttpMethod.Get, $"{RutaJornadas}/{JornadaId}"),
            (HttpMethod.Put, $"{RutaPlantillas}/{PlantillaIdCocina}/jornada"));
        JsonNode.Parse(fakes.Handler.Requests[2].Cuerpo!)!["jornadaId"]!.GetValue<string>().Should().Be(JornadaId);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(AsignarJornadaAPlantillaSemanalTool.Mensajes.ResultadoJornadaAsignada);
        json["plantilla"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be(JornadaId);
        json["jornada"]!["descripcion"]!.GetValue<string>().Should().Be(DescripcionJornada);
        json["nota"]!.GetValue<string>()
            .Should().Be(AsignarJornadaAPlantillaSemanalTool.Mensajes.NotaAdvertenciasEventuales);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_RechazaSinLlamadasHttp_CuandoJornadaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, jornadaId: "no-es-guid");

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaSemanalTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // CA-2
    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Semana Tipo Cocina");

        resultado.Should().Be(string.Format(
            AsignarJornadaAPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "Semana Tipo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // CA-3
    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_RespondePlantillaNoExisteSinPut_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(
            AsignarJornadaAPlantillaSemanalTool.Mensajes.PlantillaNoExiste, PlantillaIdCocina));
        resultado.Should().Contain(PlantillaIdCocina).And.Contain("buscar_plantillas_semanales");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_RespondeElRechazoDelDominioSinPut_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaSemanalTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_RespondeJornadaNoExisteSinPut_Cuando404DeLaJornada()
    {
        var fakes = CrearTool(statusJornada: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(AsignarJornadaAPlantillaSemanalTool.Mensajes.JornadaNoExiste);
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_TraduceElRechazoDelDominio_Cuando409PlantillaRetirada()
    {
        const string cuerpo = "La plantilla esta retirada";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarJornadaAPlantillaSemanal_TraduceElRechazoDelDominio_Cuando404DelPut()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(statusPut: HttpStatusCode.NotFound, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarJornadaAPlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
