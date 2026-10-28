using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.RetirarPlantilla;

public class RetirarPlantillaToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";

    private sealed record Fakes(RetirarPlantillaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, RutaDetalle(PlantillaId), statusDetalle,
            statusDetalle == HttpStatusCode.OK
                ? Plantilla(PlantillaId, "Ciclo Cocina", 7, true, [Dia(1, Turno("Cocina Manana"))])
                : "");
        handler.Responde(HttpMethod.Delete, $"{RutaCatalogo}/{PlantillaId}", statusDelete, cuerpoDelete);

        var api = new ProgramacionApi(cliente);
        return new Fakes(new RetirarPlantillaTool(api, new ResolutorPlantillaTurnosPorId(api)), handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string plantillaId = PlantillaId) =>
        fakes.Tool.Run(null!, plantillaId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task RetirarPlantilla_EnviaElDeleteAlIdYEcoConElNombreDelDetalle_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaDetalle(PlantillaId)),
            (HttpMethod.Delete, $"{RutaCatalogo}/{PlantillaId}"));
        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(RetirarPlantillaTool.Mensajes.ResultadoPlantillaRetirada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(PlantillaId);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["nota"]!.GetValue<string>().Should().Be(RetirarPlantillaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task RetirarPlantilla_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Ciclo Cocina");

        resultado.Should().Be(string.Format(RetirarPlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirarPlantilla_RespondePlantillaNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaTool.Mensajes.PlantillaNoExiste, PlantillaId));
        resultado.Should().Contain("buscar_plantillas");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task RetirarPlantilla_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "La plantilla ya esta retirada";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarPlantilla_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
