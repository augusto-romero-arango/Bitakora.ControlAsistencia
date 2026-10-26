using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.RetirarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.RetirarPlantillaSemanal;

public class RetirarPlantillaSemanalToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";

    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");

    private sealed record Fakes(RetirarPlantillaSemanalTool Tool, HandlerPorRuta Handler);

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
        handler.Responde(HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}", statusDelete, cuerpoDelete);

        var programacionApi = new ProgramacionApi(cliente);
        var tool = new RetirarPlantillaSemanalTool(programacionApi, new ResolutorPlantillaPorId(programacionApi));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string plantillaId = PlantillaIdCocina) =>
        fakes.Tool.Run(context: null!, plantillaId: plantillaId, ct: TestContext.Current.CancellationToken);

    // CA-4: lee el detalle por id, envia el DELETE a ese id y el eco conserva el nombre leido.
    [Fact]
    public async Task RetirarPlantillaSemanal_EnviaElDeleteAlIdYEcoConElNombreDelDetalle_CuandoLaPlantillaExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Delete && r.Ruta == $"{RutaPlantillas}/{PlantillaIdCocina}");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(RetirarPlantillaSemanalTool.Mensajes.ResultadoPlantillaRetirada);
        json["plantilla"]!["id"]!.GetValue<string>().Should().Be(PlantillaIdCocina);
        json["plantilla"]!["nombre"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["nota"]!.GetValue<string>().Should().Be(RetirarPlantillaSemanalTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-5: nunca llama al catalogo completo.
    [Fact]
    public async Task RetirarPlantillaSemanal_LeeElDetalleDeLaPlantillaPorId_SinConsultarElCatalogo()
    {
        var fakes = CrearTool();

        await Ejecutar(fakes);

        fakes.Handler.Requests.Should().Contain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina));
        fakes.Handler.Requests.Should().NotContain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDePlantillaFalso.RutaCatalogo);
    }

    // CA-3: detalle 404 -> frase con el id que remite a buscar_plantillas_semanales, sin DELETE.
    [Fact]
    public async Task RetirarPlantillaSemanal_RespondePlantillaNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.PlantillaNoExiste, PlantillaIdCocina));
        resultado.Should().Contain(PlantillaIdCocina).And.Contain("buscar_plantillas_semanales");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    // CA-2: plantilla_id que no es GUID -> mensaje del .resx con el valor, sin backend.
    [Fact]
    public async Task RetirarPlantillaSemanal_RechazaSinLlamarAlBackend_CuandoPlantillaIdNoEsUnGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Semana Tipo Cocina");

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "Semana Tipo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_RechazaSinLlamarAlBackend_CuandoPlantillaIdEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "   ");

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "   "));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirarPlantillaSemanal_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    // CA-ADR-0030: un fallo de lectura del detalle se traduce a texto y corta antes del DELETE.
    [Fact]
    public async Task RetirarPlantillaSemanal_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarPlantillaSemanalTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
