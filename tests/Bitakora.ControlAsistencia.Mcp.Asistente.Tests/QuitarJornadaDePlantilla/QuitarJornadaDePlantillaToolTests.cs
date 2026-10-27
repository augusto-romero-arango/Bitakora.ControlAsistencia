using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.QuitarJornadaDePlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.QuitarJornadaDePlantilla;

public class QuitarJornadaDePlantillaToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";

    private sealed record Fakes(QuitarJornadaDePlantillaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, RutaDetalle(PlantillaId), statusDetalle,
            statusDetalle == HttpStatusCode.OK
                ? Plantilla(PlantillaId, "Ciclo Cocina", 7, true, [Dia(1, Turno("Cocina Manana"))], conJornada: false)
                : "");
        handler.Responde(HttpMethod.Delete, $"{RutaCatalogo}/{PlantillaId}/jornada", statusDelete, cuerpoDelete);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new QuitarJornadaDePlantillaTool(api, new ResolutorPlantillaTurnosPorId(api)), handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string plantillaId = PlantillaId) =>
        fakes.Tool.Run(null!, plantillaId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task QuitarJornadaDePlantilla_EnviaElDeleteYEcoConElNombre_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaDetalle(PlantillaId)),
            (HttpMethod.Delete, $"{RutaCatalogo}/{PlantillaId}/jornada"));
        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(QuitarJornadaDePlantillaTool.Mensajes.ResultadoJornadaQuitada);
        json["plantilla"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["nota"]!.GetValue<string>().Should().Be(QuitarJornadaDePlantillaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task QuitarJornadaDePlantilla_RespondeElMismoEco_CuandoSeRepiteSinJornada()
    {
        var fakes = CrearTool();

        var primera = await Ejecutar(fakes);
        var segunda = await Ejecutar(fakes);

        segunda.Should().Be(primera);
        fakes.Handler.Requests.Count(r => r.Metodo == HttpMethod.Delete).Should().Be(2);
    }

    [Fact]
    public async Task QuitarJornadaDePlantilla_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Ciclo Cocina");

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task QuitarJornadaDePlantilla_RespondePlantillaNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaTool.Mensajes.PlantillaNoExiste, PlantillaId));
        resultado.Should().Contain("buscar_plantillas");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task QuitarJornadaDePlantilla_TraduceElRechazoDelDominio_Cuando409PorPlantillaRetirada()
    {
        const string cuerpo = "La plantilla esta retirada";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarJornadaDePlantilla_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
