using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ObtenerPlantillaSemanal;

// Tests del remodelado de obtener_plantilla_semanal (CA-2/CA-3, issue #629; identidad por plantilla_id,
// issue #1244): lee el detalle por GET programacion/plantillas-semanales/{id}, sin catalogo previo, y
// arma el cuadro por semana y dia.
public class ObtenerPlantillaSemanalToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";
    private const string PlantillaIdBodega = "01a07000-1000-7000-9000-000000000004";

    private static (ObtenerPlantillaSemanalTool Tool, HandlerPorRuta Handler) CrearTool(
        string fixtureDetalle = "obtener-plantilla-semanal.json",
        string plantillaId = PlantillaId,
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        string cuerpoDetalle = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(plantillaId), statusDetalle,
            statusDetalle == HttpStatusCode.OK ? Fixtures.Leer("ObtenerPlantillaSemanal", fixtureDetalle) : cuerpoDetalle);
        return (new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente)), handler);
    }

    private static Task<string> Ejecutar(ObtenerPlantillaSemanalTool tool, string plantillaId = PlantillaId) =>
        tool.Run(null!, plantillaId, TestContext.Current.CancellationToken);

    // CA-4 y CA-5: el cuadro sale del detalle por id, con una sola llamada y sin catalogo.
    [Fact]
    public async Task ObtenerPlantillaSemanal_ArmaElCuadroPorSemanaYDia_CuandoLaPlantillaExiste()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool);

        handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaId)));

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["id"]!.GetValue<string>().Should().Be(PlantillaId);
        json["nombre"]!.GetValue<string>().Should().Be("[TEST] Semana Cocina");
        json["semanas"]!.GetValue<int>().Should().Be(1);
        json["completa"]!.GetValue<bool>().Should().BeFalse();

        var semana = json["cuadro"]![0]!.AsObject();
        semana["semana"]!.GetValue<int>().Should().Be(1);
        semana["lunes"]!.GetValue<string>().Should().Be("[TEST] Cocina Manana 07:00-17:00");
        semana["martes"]!.GetValue<string>().Should().Be("[TEST] Cocina Tarde 13:00-21:00");
        semana["viernes"]!.GetValue<string>().Should().Be("[TEST] Cocina Noche 21:00-05:00+1");
        semana["sabado"]!.GetValue<string>().Should().Be("[TEST] Descanso Semana Cocina (descanso)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MuestraSinTurno_CuandoElDiaEstaVacio()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool);

        JsonNode.Parse(resultado)!["cuadro"]![0]!["domingo"]!.GetValue<string>().Should().Be("sin turno");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MarcaTurnoIncompleto_CuandoElDiaTieneUnTurnoIncompleto()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool);

        JsonNode.Parse(resultado)!["cuadro"]![0]!["miercoles"]!.GetValue<string>()
            .Should().Be("[TEST] Turno Incompleto (turno incompleto)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MarcaTurnoRetirado_CuandoElDiaTieneUnTurnoRetirado()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool);

        JsonNode.Parse(resultado)!["cuadro"]![0]!["jueves"]!.GetValue<string>()
            .Should().Be("01a07000-2000-7000-9000-000000000004 (turno retirado)");
    }

    // CA-3: 404 del detalle -> frase con el id que remite a buscar_plantillas_semanales.
    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondePlantillaNoExiste_Cuando404EnElDetalle()
    {
        var (tool, handler) = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(tool);

        handler.Requests.Should().HaveCount(1);
        resultado.Should().Be(string.Format(ObtenerPlantillaSemanalTool.Mensajes.PlantillaNoExiste, PlantillaId));
        resultado.Should().Contain(PlantillaId).And.Contain("buscar_plantillas_semanales");
    }

    // CA-2: plantilla_id que no es GUID -> mensaje del .resx con el valor, sin backend.
    [Fact]
    public async Task ObtenerPlantillaSemanal_RechazaSinLlamarAlBackend_CuandoPlantillaIdNoEsUnGuid()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, plantillaId: "[TEST] Semana Cocina");

        resultado.Should().Be(string.Format(ObtenerPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "[TEST] Semana Cocina"));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_RechazaSinLlamarAlBackend_CuandoPlantillaIdEstaEnBlanco()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, plantillaId: "   ");

        resultado.Should().Be(string.Format(ObtenerPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "   "));
        handler.Requests.Should().BeEmpty();
    }

    // Enumerable.Range(1, detalle.Semanas) -- no las semanas presentes en Dias: una semana sin
    // ningun turno asignado tambien debe rendir sus 7 "sin turno" (CA-2).
    [Fact]
    public async Task ObtenerPlantillaSemanal_RindeUnaFilaPorSemanaDeclarada_CuandoUnaSemanaNoTieneNingunDiaAsignado()
    {
        var (tool, _) = CrearTool("obtener-plantilla-semanal-dos-semanas.json", PlantillaIdBodega);

        var resultado = await Ejecutar(tool, PlantillaIdBodega);

        var cuadro = JsonNode.Parse(resultado)!["cuadro"]!.AsArray();
        cuadro.Should().HaveCount(2);
        cuadro[0]!["lunes"]!.GetValue<string>().Should().Be("[TEST] Bodega Manana (08:00-16:00)");

        var segunda = cuadro[1]!.AsObject();
        segunda["semana"]!.GetValue<int>().Should().Be(2);
        new[] { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" }
            .Select(dia => segunda[dia]!.GetValue<string>())
            .Should().AllBe("sin turno");
    }

    // CA-ADR-0030: el 5xx del detalle se traduce a RechazoDelDominio con el cuerpo crudo.
    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondeRechazoDelDominio_CuandoElDetalleFallaConError500()
    {
        var (tool, _) = CrearTool(
            statusDetalle: HttpStatusCode.InternalServerError, cuerpoDetalle: "detalle no disponible");

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(
            ObtenerPlantillaSemanalTool.Mensajes.RechazoDelDominio, "detalle no disponible"));
    }
}
