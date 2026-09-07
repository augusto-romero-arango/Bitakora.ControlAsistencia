using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.RetirarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.RetirarPlantillaSemanal;

public class RetirarPlantillaSemanalToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";

    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");

    private sealed record Fakes(RetirarPlantillaSemanalTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        string? plantillasJson = null,
        HttpStatusCode statusPlantillas = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaPlantillas, statusPlantillas, plantillasJson ?? PlantillasJson);
        handler.Responde(HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}", statusDelete, cuerpoDelete);

        var tool = new RetirarPlantillaSemanalTool(new ProgramacionApi(cliente));
        return new Fakes(tool, handler);
    }

    // CA-5: resuelve "Semana Tipo Cocina" en el catalogo y envia el DELETE al id correspondiente.
    [Fact]
    public async Task RetirarPlantillaSemanal_ResuelveElIdPorNombreYEnviaElDelete_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Delete && r.Ruta == $"{RutaPlantillas}/{PlantillaIdCocina}");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(RetirarPlantillaSemanalTool.Mensajes.ResultadoPlantillaRetirada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(PlantillaIdCocina);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["nota"]!.GetValue<string>().Should().Be(RetirarPlantillaSemanalTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-5: coincidencia exacta bajo trim + colapso de espacios + case-insensitive; acentos
    // significativos (mismo criterio que ResolutorTurnoPorNombre).
    [Fact]
    public async Task RetirarPlantillaSemanal_ResuelveLaPlantilla_ConTrimColapsoDeEspaciosYCaseInsensitive()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "  semana   tipo   cocina  ", TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_RespondePlantillaNoExisteSinDelete_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Plantilla Que No Existe", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            RetirarPlantillaSemanalTool.Mensajes.PlantillaNoExiste,
            "Plantilla Que No Existe", "Semana Tipo Cocina, Turno Rotativo"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_RechazaSinLlamarAlDominio_CuandoLaPlantillaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(null!, "   ", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.CampoObligatorio, "plantilla"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // Boundary del sistema: un fallo del catalogo se traduce a texto y corta antes del DELETE
    // (CA-ADR-0030), mismo criterio que retirar_turno.
    [Fact]
    public async Task RetirarPlantillaSemanal_RespondeElRechazoDelDominioSinDelete_CuandoElCatalogoFalla()
    {
        var fakes = CrearTool(plantillasJson: "", statusPlantillas: HttpStatusCode.ServiceUnavailable);

        var resultado = await fakes.Tool.Run(
            null!, "Semana Tipo Cocina", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
