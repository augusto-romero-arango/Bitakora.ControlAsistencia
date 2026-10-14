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

    private static Fakes CrearTool(HttpStatusCode statusDelete = HttpStatusCode.NoContent, string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaPlantillas, HttpStatusCode.OK, PlantillasJson);
        handler.Responde(HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/jornada", statusDelete, cuerpoDelete);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new QuitarJornadaDePlantillaSemanalTool(api, new ResolutorPlantillaPorNombre(api)), handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string plantilla = "Semana Tipo Cocina") =>
        fakes.Tool.Run(null!, plantilla, TestContext.Current.CancellationToken);

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_ResuelveLaPlantillaYHaceDelete_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaPlantillas),
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
    public async Task QuitarJornadaDePlantillaSemanal_RechazaSinLlamadasHttp_CuandoPlantillaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantilla: "  ");

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.CampoObligatorio, "plantilla"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_RespondePlantillaNoExisteSinDelete_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantilla: "Plantilla Que No Existe");

        resultado.Should().Be(string.Format(
            QuitarJornadaDePlantillaSemanalTool.Mensajes.PlantillaNoExiste,
            "Plantilla Que No Existe", "Semana Tipo Cocina, Turno Rotativo"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_TraduceElRechazoDelDominio_Cuando409PlantillaRetirada()
    {
        const string cuerpo = "La plantilla esta retirada";
        var fakes = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarJornadaDePlantillaSemanal_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(HttpStatusCode.NotFound, cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(QuitarJornadaDePlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
