using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Ejemplo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Ejemplo.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Ejemplo;

public class EjemploListarToolTests
{
    private static async Task<JsonNode> Ejecutar(
        string fixture, string? filtroNombre = null, string? fechaReferencia = null)
    {
        var cliente = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new EjemploListarTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, fechaReferencia, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task EjemploListar_RemodelaCadaElementoAIdYNombre_CuandoElCatalogoResponde()
    {
        var json = await Ejecutar("catalogo.json");

        json["total"]!.GetValue<int>().Should().Be(4);
        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");
        json.AsObject().ContainsKey("fechaReferencia").Should().BeFalse("sin el parametro, el eco no viaja");

        var primero = json["elementos"]![0]!.AsObject();
        primero["id"]!.GetValue<string>().Should().Be("elem-001");
        primero["nombre"]!.GetValue<string>().Should().Be("Elemento Uno", "el nombre viaja sin el padding del catalogo");
        primero.ContainsKey("detalle").Should().BeFalse("el detalle interno no viaja en el resumen");
    }

    [Fact]
    public async Task EjemploListar_TruncaConSenal_CuandoElCatalogoExcedeElMaximo()
    {
        var json = await Ejecutar("catalogo-grande.json");

        json["total"]!.GetValue<int>().Should().Be(60);
        json["mostrando"]!.GetValue<int>().Should().Be(EjemploListarTool.MaximoElementos);
        json["elementos"]!.AsArray().Should().HaveCount(EjemploListarTool.MaximoElementos);
        json["nota"]!.GetValue<string>().Should().Contain("50 de 60");
    }

    [Fact]
    public async Task EjemploListar_FiltraSinAcentosNiMayusculas_CuandoRecibeFiltroNombre()
    {
        var json = await Ejecutar("catalogo.json", filtroNombre: "nandu");

        json["total"]!.GetValue<int>().Should().Be(1, "'nandu' debe encontrar 'Ñandú'");
        json["elementos"]![0]!["nombre"]!.GetValue<string>().Should().Contain("Ñandú");
    }

    [Fact]
    public async Task EjemploListar_RespondeElMensajeDeValidacion_CuandoElFiltroExcedeElLargoMaximo()
    {
        var cliente = ClienteFalso.Con(Fixtures.Leer("catalogo.json"));
        var tool = new EjemploListarTool(new ProgramacionApi(cliente));
        var filtroDemasiadoLargo = new string('a', EjemploListarTool.MaximoLargoFiltro + 1);

        var resultado = await tool.Run(null!, filtroDemasiadoLargo, null, TestContext.Current.CancellationToken);

        resultado.Should().Be("El filtro no puede superar 100 caracteres.");
    }

    [Fact]
    public async Task EjemploListar_HaceEcoDeLaFechaDeReferencia_CuandoLaFechaEsValida()
    {
        var json = await Ejecutar("catalogo.json", fechaReferencia: "2026-09-01");

        json["fechaReferencia"]!.GetValue<string>().Should().Be("2026-09-01");
    }

    [Fact]
    public async Task EjemploListar_RespondeElMensajeDeValidacion_CuandoLaFechaDeReferenciaEsInvalida()
    {
        var cliente = ClienteFalso.Con(Fixtures.Leer("catalogo.json"));
        var tool = new EjemploListarTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, null, "2026-99-99", TestContext.Current.CancellationToken);

        resultado.Should().Be("'fecha_referencia' debe tener formato yyyy-MM-dd; llego '2026-99-99'.");
    }
}
