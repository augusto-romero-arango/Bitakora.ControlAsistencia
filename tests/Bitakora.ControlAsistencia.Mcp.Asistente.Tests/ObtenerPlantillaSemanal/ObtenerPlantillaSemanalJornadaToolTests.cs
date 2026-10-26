using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ObtenerPlantillaSemanal;

public class ObtenerPlantillaSemanalJornadaToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";

    private static async Task<JsonObject> Obtener(string fixtureDetalle)
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaId), System.Net.HttpStatusCode.OK,
            Fixtures.Leer("ObtenerPlantillaSemanal", fixtureDetalle));
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, PlantillaId, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!.AsObject();
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_ExponeJornadaYAdvertenciasEnOrden_CuandoElCuadroLasTrae()
    {
        var json = await Obtener("obtener-plantilla-semanal-con-jornada.json");

        json["jornada"]!["jornada_id"]!.GetValue<string>().Should().Be("01a08700-0000-7000-9000-000000000001");
        json["jornada"]!["descripcion"]!.GetValue<string>().Should().Be(
            "42:00 semanales, tope diario 8:00, minimo diario 0:00, 1 dia de descanso por semana");
        json["advertencias"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal(
            "Semana 1, lunes: el turno supera el tope diario en 2:00.",
            "Semana 1: faltan 1 dias de descanso.");
        json["cuadro"]![0]!["lunes"]!.GetValue<string>().Should().Be("[TEST] Cocina Manana (07:00-17:00)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_OmiteJornadaYDevuelveAdvertenciasVacias_CuandoElCuadroNoLasTrae()
    {
        var json = await Obtener("obtener-plantilla-semanal.json");

        json.ContainsKey("jornada").Should().BeFalse();
        json["advertencias"]!.AsArray().Should().BeEmpty();
    }
}
