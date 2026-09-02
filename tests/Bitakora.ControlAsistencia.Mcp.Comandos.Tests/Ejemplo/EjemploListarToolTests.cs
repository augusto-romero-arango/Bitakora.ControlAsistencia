using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Ejemplo;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Ejemplo.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Ejemplo;

public class EjemploListarToolTests
{
    private static async Task<JsonNode> Ejecutar(string fixture, string? filtroNombre = null)
    {
        var cliente = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new EjemploListarTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task EjemploListar_RemodelaCadaElementoAIdYNombre_CuandoElCatalogoResponde()
    {
        var json = await Ejecutar("catalogo.json");

        json["total"]!.GetValue<int>().Should().Be(4);
        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");

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

        var resultado = await tool.Run(null!, filtroDemasiadoLargo, TestContext.Current.CancellationToken);

        resultado.Should().Be("El filtro no puede superar 100 caracteres.");
    }
}
