using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ObtenerPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.ObtenerPlantillaSemanal;

// Tests del remodelado de obtener_plantilla_semanal (CA-2/CA-3, issue #629): resuelve el nombre
// contra GET programacion/plantillas-semanales y arma el cuadro con GET
// programacion/plantillas-semanales/{id}. El HandlerFuncional responde distinto segun la ruta,
// para poder simular la carrera lista-encuentra/detalle-404 y contar cuantos GET se hicieron.
public class ObtenerPlantillaSemanalToolTests
{
    private const string RutaListado = "/api/programacion/plantillas-semanales";

    private static (HttpClient Cliente, HandlerFuncional Handler) ClienteConCatalogoYDetalle(
        string fixtureListado, string fixtureDetalle, HttpStatusCode statusDetalle = HttpStatusCode.OK)
    {
        return ClienteFalso.ConFuncion(req => req.RequestUri!.AbsolutePath == RutaListado
            ? ClienteFalso.JsonOk(Fixtures.Leer(fixtureListado))
            : statusDetalle == HttpStatusCode.NotFound
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : ClienteFalso.JsonOk(Fixtures.Leer(fixtureDetalle)));
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_ArmaElCuadroPorSemanaYDia_CuandoLaPlantillaExiste()
    {
        var (cliente, handler) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(2, "resuelve nombre -> id y luego pide el detalle");
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be(RutaListado);
        handler.Requests[1].RequestUri!.AbsolutePath.Should()
            .Be("/api/programacion/plantillas-semanales/01a07000-1000-7000-9000-000000000001");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["id"]!.GetValue<string>().Should().Be("01a07000-1000-7000-9000-000000000001");
        json["nombre"]!.GetValue<string>().Should().Be("[TEST] Semana Cocina");
        json["semanas"]!.GetValue<int>().Should().Be(1);
        json["completa"]!.GetValue<bool>().Should().BeFalse();

        var semana = json["cuadro"]![0]!.AsObject();
        semana["semana"]!.GetValue<int>().Should().Be(1);
        semana["lunes"]!.GetValue<string>().Should().Be("[TEST] Cocina Manana (07:00-17:00)");
        semana["martes"]!.GetValue<string>().Should().Be("[TEST] Cocina Tarde (13:00-21:00)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MuestraSinTurno_CuandoElDiaEstaVacio()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        var semana = JsonNode.Parse(resultado)!["cuadro"]![0]!.AsObject();
        semana["domingo"]!.GetValue<string>().Should().Be("sin turno");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MarcaTurnoIncompleto_CuandoElDiaTieneUnTurnoIncompleto()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        var semana = JsonNode.Parse(resultado)!["cuadro"]![0]!.AsObject();
        semana["miercoles"]!.GetValue<string>().Should().Be("[TEST] Turno Incompleto (turno incompleto)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_MarcaTurnoRetirado_CuandoElDiaTieneUnTurnoRetirado()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        var semana = JsonNode.Parse(resultado)!["cuadro"]![0]!.AsObject();
        semana["jueves"]!.GetValue<string>().Should().Be("01a07000-2000-7000-9000-000000000004 (turno retirado)");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_ResuelveElNombreConEspaciosYMayusculasDistintas_CuandoNoEsExacto()
    {
        var (cliente, handler) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "  [TEST]   semana   cocina  ", TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(2);
        JsonNode.Parse(resultado)!["nombre"]!.GetValue<string>().Should().Be("[TEST] Semana Cocina");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_NoResuelve_CuandoElNombreDifiereSoloPorAcentos()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cafeteria", TestContext.Current.CancellationToken);

        resultado.Should().Contain("[TEST] Semana Cafeteria", "los acentos son significativos en la resolucion por nombre");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondeNoExiste_CuandoElNombreNoEstaEnElCatalogo_SinSegundoGet()
    {
        var (cliente, handler) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Plantilla Inexistente", TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(1, "sin match no hay razon para pedir el detalle");
        resultado.Should().Contain("[TEST] Plantilla Inexistente").And.Contain("[TEST] Semana Cocina");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondeNoExiste_CuandoElDetalleResponde404PorCarrera()
    {
        var (cliente, handler) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json", HttpStatusCode.NotFound);
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(2, "el 404 llega recien en el segundo GET");
        resultado.Should().Contain("[TEST] Semana Cocina");
    }

    // Enumerable.Range(1, detalle.Semanas) -- no las semanas presentes en Dias: una semana sin
    // ningun turno asignado tambien debe rendir sus 7 "sin turno" (CA-2).
    [Fact]
    public async Task ObtenerPlantillaSemanal_RindeUnaFilaPorSemanaDeclarada_CuandoUnaSemanaNoTieneNingunDiaAsignado()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal-dos-semanas.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Bodega", TestContext.Current.CancellationToken);

        var cuadro = JsonNode.Parse(resultado)!["cuadro"]!.AsArray();
        cuadro.Should().HaveCount(2);
        cuadro[0]!["lunes"]!.GetValue<string>().Should().Be("[TEST] Bodega Manana (08:00-16:00)");

        var segunda = cuadro[1]!.AsObject();
        segunda["semana"]!.GetValue<int>().Should().Be(2);
        new[] { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" }
            .Select(dia => segunda[dia]!.GetValue<string>())
            .Should().AllBe("sin turno");
    }

    // El boundary del sistema (5xx del Function App) se traduce a RechazoDelDominio con el cuerpo
    // crudo, nunca a una excepcion cruda en la tool call (CA-ADR-0030, MEF-ADR-0009).
    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondeRechazoDelDominio_CuandoElCatalogoFallaConError500()
    {
        var (cliente, handler) = ClienteFalso.ConFuncion(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("catalogo no disponible")
        });
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(
            null!, "[TEST] Semana Cocina", TestContext.Current.CancellationToken);

        handler.Requests.Should().HaveCount(1, "sin catalogo no hay id que pedir");
        resultado.Should().Contain("catalogo no disponible");
    }

    [Fact]
    public async Task ObtenerPlantillaSemanal_RespondeCampoObligatorio_CuandoPlantillaEstaEnBlanco()
    {
        var (cliente, _) = ClienteConCatalogoYDetalle(
            "listar-plantillas-semanales.json", "obtener-plantilla-semanal.json");
        var tool = new ObtenerPlantillaSemanalTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, "   ", TestContext.Current.CancellationToken);

        resultado.Should().Contain("plantilla").And.Contain("obligatorio");
    }
}
