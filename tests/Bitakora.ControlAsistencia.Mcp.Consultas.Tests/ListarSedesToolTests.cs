using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarSedes;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ListarSedesToolTests
{
    private static async Task<(JsonNode Json, HandlerEnlatado Handler)> Ejecutar(
        string fixture, string? filtroNombre = null)
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new ListarSedesTool(new SedesApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, TestContext.Current.CancellationToken);

        return (JsonNode.Parse(resultado)!, handler);
    }

    [Fact]
    public async Task ListarSedes_PideSoloLasActivasAlDominio_CuandoSeInvoca()
    {
        var (_, handler) = await Ejecutar("listar-sedes.json");

        handler.UltimaRequest!.RequestUri!.PathAndQuery.Should()
            .Be("/api/sedes/fichas?activa=true", "revision del PR #512: solo sedes activas");
    }

    [Fact]
    public async Task ListarSedes_RemodelaYPodaLosCamposInternos_CuandoHaySedes()
    {
        var (json, _) = await Ejecutar("listar-sedes.json");

        json["total"]!.GetValue<int>().Should().Be(2);

        var primera = json["sedes"]![0]!.AsObject();
        primera["codigo"]!.GetValue<string>().Should()
            .Be("TEST-01a04653-70e8-77bd-8f99-342a693076ab");
        primera["nombre"]!.GetValue<string>().Should().Be("[TEST] Sede Norte");
        primera["ciudad"]!.GetValue<string>().Should().Be("Bogota");
        primera["direccion"]!.GetValue<string>().Should().Be("Calle 1 # 2-3");
        primera.ContainsKey("id").Should().BeFalse("el stream key 's:{codigo}' es interno (CA-ADR-0031)");
        primera.ContainsKey("dispositivos").Should().BeFalse();
        primera.ContainsKey("centroDeCostos").Should().BeFalse();
        primera.ContainsKey("activa").Should()
            .BeFalse("toda sede que viaja es activa tras el filtro upstream: la bandera seria ruido");
    }

    [Fact]
    public async Task ListarSedes_OmiteLosCamposNulosYNoTrunca_CuandoLaSedeNoLosTiene()
    {
        var (json, _) = await Ejecutar("listar-sedes.json");

        var minima = json["sedes"]![1]!.AsObject();
        minima.ContainsKey("ciudad").Should().BeFalse();
        minima.ContainsKey("direccion").Should().BeFalse();

        json.AsObject().ContainsKey("nota").Should().BeFalse("las sedes no tienen tope de truncado");
        json.AsObject().ContainsKey("mostrando").Should().BeFalse();
    }

    [Fact]
    public async Task ListarSedes_FiltraPorNombre_CuandoRecibeFiltroNombre()
    {
        var (json, _) = await Ejecutar("listar-sedes.json", filtroNombre: "norte");

        json["total"]!.GetValue<int>().Should().Be(1);
        json["sedes"]![0]!["nombre"]!.GetValue<string>().Should().Be("[TEST] Sede Norte");
    }
}
