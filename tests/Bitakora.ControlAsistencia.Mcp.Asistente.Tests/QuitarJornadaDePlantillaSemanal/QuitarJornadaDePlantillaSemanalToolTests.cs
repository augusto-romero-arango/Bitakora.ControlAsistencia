using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.QuitarJornadaDePlantillaSemanal;

public class QuitarJornadaDePlantillaSemanalToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";

    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");

    private sealed record Fakes(QuitarJornadaDePlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        if (statusDetalle == HttpStatusCode.OK)
            handler.RespondeDetalleDePlantilla(PlantillasJson, PlantillaIdCocina);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina), statusDetalle, "");
        handler.Responde(HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/jornada", statusDelete, cuerpoDelete);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new QuitarJornadaDePlantillaSemanalTool(api, new ResolutorPlantillaPorId(api)), handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string plantillaId = PlantillaIdCocina) =>
        fakes.Tool.Run(null!, plantillaId, TestContext.Current.CancellationToken);

    // CA-4 y CA-5: detalle por id (sin catalogo), DELETE al id y eco con el nombre del detalle.
    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_LeeElDetalleYHaceDelete_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina)),
            (HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/jornada"));

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(QuitarJornadaDePlantillaSemanalTool.Mensajes.ResultadoJornadaQuitada);
        json["plantilla"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["nota"]!.GetValue<string>()
            .Should().Be(QuitarJornadaDePlantillaSemanalTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_RespondeExito_CuandoLaPlantillaNoTeniaJornada()
    {
        var fakes = CrearTool(statusDelete: HttpStatusCode.NoContent);

        var resultado = await Ejecutar(fakes);

        JsonNode.Parse(resultado)!["resultado"]!.GetValue<string>()
            .Should().Be(QuitarJornadaDePlantillaSemanalTool.Mensajes.ResultadoJornadaQuitada);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Semana Tipo Cocina");

        resultado.Should().Be(string.Format(
            QuitarJornadaDePlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "Semana Tipo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_RespondePlantillaNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(
            QuitarJornadaDePlantillaSemanalTool.Mensajes.PlantillaNoExiste, PlantillaIdCocina));
        resultado.Should().Contain(PlantillaIdCocina).And.Contain("buscar_plantillas_semanales");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_TraduceElRechazoDelDominio_Cuando409PlantillaRetirada()
    {
        const string cuerpo = "La plantilla esta retirada";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
