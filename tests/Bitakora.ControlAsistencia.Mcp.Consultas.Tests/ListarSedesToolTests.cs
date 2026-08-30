using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarSedes;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ListarSedesToolTests
{
    private static async Task<JsonNode> Ejecutar(string fixture, string? filtroNombre = null)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new ListarSedesTool(new SedesApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task ListarSedes_RemodelaYPodaLosCamposInternos_CuandoHaySedes()
    {
        var json = await Ejecutar("listar-sedes.json");

        json["total"]!.GetValue<int>().Should().Be(5);

        var primera = json["sedes"]![0]!.AsObject();
        primera["codigo"]!.GetValue<string>().Should()
            .Be("TEST-01a04653-70e8-77bd-8f99-342a693076ab");
        primera["nombre"]!.GetValue<string>().Should().Be("[TEST] Sede Norte");
        primera["ciudad"]!.GetValue<string>().Should().Be("Bogota");
        primera["direccion"]!.GetValue<string>().Should().Be("Calle 1 # 2-3");
        primera["activa"]!.GetValue<bool>().Should().BeTrue();
        primera.ContainsKey("id").Should().BeFalse("el stream key 's:{codigo}' es interno (CA-ADR-0031)");
        primera.ContainsKey("dispositivos").Should().BeFalse();
        primera.ContainsKey("centroDeCostos").Should().BeFalse();
    }

    [Fact]
    public async Task ListarSedes_OmiteLosCamposNulos_CuandoLaSedeNoLosTiene()
    {
        var json = await Ejecutar("listar-sedes.json");

        var minima = json["sedes"]![1]!.AsObject();
        minima.ContainsKey("ciudad").Should().BeFalse();
        minima.ContainsKey("direccion").Should().BeFalse();

        json["sedes"]![2]!["activa"]!.GetValue<bool>().Should().BeFalse("la tercera sede esta inactiva");
    }

    [Fact]
    public async Task ListarSedes_TruncaConSenal_CuandoElCatalogoExcedeElMaximo()
    {
        var json = await Ejecutar("listar-sedes-catalogo-grande.json");

        json["mostrando"]!.GetValue<int>().Should().Be(ListarSedesTool.MaximoSedes);
        json["sedes"]!.AsArray().Should().HaveCount(ListarSedesTool.MaximoSedes);
        json["nota"]!.GetValue<string>().Should().Contain("50 de 60");
    }

    [Fact]
    public async Task ListarSedes_FiltraPorNombre_CuandoRecibeFiltroNombre()
    {
        var json = await Ejecutar("listar-sedes.json", filtroNombre: "norte");

        json["total"]!.GetValue<int>().Should().Be(1);
        json["sedes"]![0]!["nombre"]!.GetValue<string>().Should().Be("[TEST] Sede Norte");
    }
}
