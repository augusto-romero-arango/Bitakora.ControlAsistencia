using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ObtenerPlantilla;

public class ObtenerPlantillaToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";

    private static string Detalle(bool conJornada = true) => Plantilla(
        PlantillaId, "Ciclo Cocina", 5, false,
        [
            Dia(1, Turno("Cocina Manana", descripcion: "07:00-17:00")),
            Dia(2, Turno("Turno Vacio", completo: false, descripcion: "(incompleto)")),
            Dia(3, Turno("Turno Viejo", completo: false, retirado: true)),
            Dia(5, Turno("Cocina Noche", descripcion: "21:00-05:00+1"))
        ],
        advertencias: 2, conJornada: conJornada);

    private static (ObtenerPlantillaTool Tool, HandlerPorRuta Handler) CrearTool(
        string cuerpo, HttpStatusCode status = HttpStatusCode.OK)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaDetalle(PlantillaId), status, cuerpo);
        return (new ObtenerPlantillaTool(new ProgramacionApi(cliente)), handler);
    }

    private static Task<string> Ejecutar(ObtenerPlantillaTool tool, string plantillaId = PlantillaId) =>
        tool.Run(null!, plantillaId, TestContext.Current.CancellationToken);

    private static string TextoDelDia(JsonNode json, int dia) =>
        json["dias"]!.AsArray().Single(d => d!["dia"]!.GetValue<int>() == dia)!["turno"]!.GetValue<string>();

    [Fact]
    public async Task ObtenerPlantilla_ArmaLosDiasUnoANEnOrdenConNombreYHorario_CuandoLaPlantillaExiste()
    {
        var (tool, handler) = CrearTool(Detalle());

        var json = JsonNode.Parse(await Ejecutar(tool))!;

        handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal((HttpMethod.Get, RutaDetalle(PlantillaId)));
        json["id"]!.GetValue<string>().Should().Be(PlantillaId);
        json["nombre"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["diasDelCiclo"]!.GetValue<int>().Should().Be(5);
        json["completa"]!.GetValue<bool>().Should().BeFalse();
        json["dias"]!.AsArray().Select(d => d!["dia"]!.GetValue<int>()).Should().Equal(1, 2, 3, 4, 5);
        TextoDelDia(json, 1).Should().Be("Cocina Manana 07:00-17:00");
        TextoDelDia(json, 5).Should().Be("Cocina Noche 21:00-05:00+1");
    }

    [Fact]
    public async Task ObtenerPlantilla_MuestraSinTurno_CuandoElDiaEstaVacio()
    {
        var (tool, _) = CrearTool(Detalle());

        var json = JsonNode.Parse(await Ejecutar(tool))!;

        TextoDelDia(json, 4).Should().Be(ObtenerPlantillaTool.Mensajes.SinTurno);
    }

    [Fact]
    public async Task ObtenerPlantilla_MarcaTurnoIncompleto_CuandoElDiaTieneUnTurnoIncompleto()
    {
        var (tool, _) = CrearTool(Detalle());

        var json = JsonNode.Parse(await Ejecutar(tool))!;

        TextoDelDia(json, 2).Should().Be(string.Format(ObtenerPlantillaTool.Mensajes.TurnoIncompleto, "Turno Vacio"));
    }

    [Fact]
    public async Task ObtenerPlantilla_MarcaRetiradoYNoIncompleto_CuandoElTurnoEstaRetiradoEIncompleto()
    {
        var (tool, _) = CrearTool(Detalle());

        var json = JsonNode.Parse(await Ejecutar(tool))!;

        TextoDelDia(json, 3).Should().Be(string.Format(ObtenerPlantillaTool.Mensajes.TurnoRetirado, "Turno Viejo"));
    }

    [Fact]
    public async Task ObtenerPlantilla_ResumeLaJornadaYDescribeLasAdvertencias_CuandoLaPlantillaLasTiene()
    {
        var (tool, _) = CrearTool(Detalle());

        var json = JsonNode.Parse(await Ejecutar(tool))!;

        json["jornada"]!["descripcion"]!.GetValue<string>().Should().Be("Jornada de prueba");
        json["advertencias"]!.AsArray().Select(a => a!.GetValue<string>())
            .Should().Equal("Advertencia 1", "Advertencia 2");
    }

    [Fact]
    public async Task ObtenerPlantilla_OmiteLaJornada_CuandoLaPlantillaNoTieneJornada()
    {
        var (tool, _) = CrearTool(Detalle(conJornada: false));

        var json = JsonNode.Parse(await Ejecutar(tool))!.AsObject();

        json.ContainsKey("jornada").Should().BeFalse();
    }

    [Fact]
    public async Task ObtenerPlantilla_RemiteABuscarPlantillasSinLlamarAlServidor_CuandoElIdNoEsGuid()
    {
        var (tool, handler) = CrearTool(Detalle());

        var resultado = await Ejecutar(tool, "Ciclo Cocina");

        resultado.Should().Be(string.Format(ObtenerPlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo Cocina"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerPlantilla_RemiteABuscarPlantillas_CuandoLaPlantillaNoExiste()
    {
        var (tool, _) = CrearTool("", HttpStatusCode.NotFound);

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(ObtenerPlantillaTool.Mensajes.PlantillaNoExiste, PlantillaId));
    }

    [Fact]
    public async Task ObtenerPlantilla_RespondeRechazoDelDominio_CuandoElDetalleFallaConError()
    {
        var (tool, _) = CrearTool("detalle no disponible", HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(ObtenerPlantillaTool.Mensajes.RechazoDelDominio, "detalle no disponible"));
    }
}
