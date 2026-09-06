using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarPlantillasSemanales;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.ListarPlantillasSemanales;

// Tests del remodelado de listar_plantillas_semanales (CA-1, issue #629): dada la respuesta JSON
// real de #625, la tool produce la forma compacta esperada. Protegen contra roturas silenciosas
// del mapeo cuando cambie el contrato upstream.
public class ListarPlantillasSemanalesToolTests
{
    private static async Task<JsonNode> Ejecutar(string fixture, string? filtroNombre = null)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new ListarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task ListarPlantillasSemanales_RemodelaCadaPlantillaAIdNombreYSemanas_CuandoElCatalogoResponde()
    {
        var json = await Ejecutar("listar-plantillas-semanales.json");

        json["total"]!.GetValue<int>().Should().Be(4);
        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");

        var primera = json["plantillas"]![0]!.AsObject();
        primera["id"]!.GetValue<string>().Should().Be("01a07000-1000-7000-9000-000000000001");
        primera["nombre"]!.GetValue<string>().Should().Be("[TEST] Semana Cocina");
        primera["semanas"]!.GetValue<int>().Should().Be(1);
        primera.ContainsKey("incompleta").Should().BeFalse("incompleta solo viaja cuando la plantilla esta incompleta");
    }

    [Fact]
    public async Task ListarPlantillasSemanales_ConservaElOrdenDelUpstream_CuandoElCatalogoResponde()
    {
        var json = await Ejecutar("listar-plantillas-semanales.json");

        json["plantillas"]!.AsArray().Select(p => p!["nombre"]!.GetValue<string>()).Should().Equal(
            "[TEST] Semana Cocina",
            "[TEST]  semana   ASEO incompleta",
            "[TEST] Semana Bodega",
            "[TEST] Semana Cafetería");
    }

    [Fact]
    public async Task ListarPlantillasSemanales_MarcaIncompleta_CuandoLaPlantillaNoEstaCompleta()
    {
        var json = await Ejecutar("listar-plantillas-semanales.json");

        var incompleta = json["plantillas"]![1]!.AsObject();
        incompleta["incompleta"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task ListarPlantillasSemanales_TruncaConSenal_CuandoElCatalogoExcedeElMaximo()
    {
        var json = await Ejecutar("listar-plantillas-semanales-grande.json");

        json["total"]!.GetValue<int>().Should().Be(60);
        json["mostrando"]!.GetValue<int>().Should().Be(ListarPlantillasSemanalesTool.MaximoPlantillas);
        json["plantillas"]!.AsArray().Should().HaveCount(ListarPlantillasSemanalesTool.MaximoPlantillas);
        json["nota"]!.GetValue<string>().Should().Contain("50 de 60");
    }

    [Fact]
    public async Task ListarPlantillasSemanales_FiltraSinAcentosNiMayusculas_CuandoRecibeFiltroNombre()
    {
        var json = await Ejecutar("listar-plantillas-semanales.json", filtroNombre: "cafeteria");

        json["total"]!.GetValue<int>().Should().Be(1, "'cafeteria' debe encontrar 'Cafetería'");
        json["plantillas"]![0]!["nombre"]!.GetValue<string>().Should().Contain("Cafetería");
    }
}
