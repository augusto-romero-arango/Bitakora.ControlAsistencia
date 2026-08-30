using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarTurnos;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

// Tests del remodelado de listar_turnos (CA-4, issue #502): dada la respuesta JSON real del
// endpoint de dev, la tool produce la forma compacta esperada. Protegen contra roturas
// silenciosas del mapeo cuando cambie el contrato upstream.
public class ListarTurnosToolTests
{
    private static async Task<JsonNode> Ejecutar(string fixture, string? filtroNombre = null)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer(fixture));
        var tool = new ListarTurnosTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, filtroNombre, TestContext.Current.CancellationToken);

        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task ListarTurnos_RemodelaCadaTurnoAIdNombreYHorario_CuandoElCatalogoResponde()
    {
        var json = await Ejecutar("listar-turnos.json");

        json["total"]!.GetValue<int>().Should().Be(4);
        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");

        var primero = json["turnos"]![0]!.AsObject();
        primero["id"]!.GetValue<string>().Should().Be("01a04fd9-8876-77e1-9aa8-8853dc72191e");
        primero["nombre"]!.GetValue<string>().Should()
            .Be("[TEST]  limpieza   MANANA 01a04fd9-811b-7c71-be0c-fb5b28ce62b1",
                "el nombre viaja sin el padding que trae el catalogo");
        primero["horario"]!.GetValue<string>().Should().Be("08:00-16:00");
        primero.ContainsKey("franjas").Should().BeFalse("el detalle de franjas es de obtener_turno");
        primero.ContainsKey("descripcion").Should().BeFalse();
        primero.ContainsKey("esDescanso").Should().BeFalse("esDescanso solo viaja cuando es true");
    }

    [Fact]
    public async Task ListarTurnos_MarcaElDescanso_CuandoElTurnoEsDescanso()
    {
        var json = await Ejecutar("listar-turnos.json");

        var descanso = json["turnos"]![2]!.AsObject();
        descanso["esDescanso"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task ListarTurnos_TruncaConSenal_CuandoElCatalogoExcedeElMaximo()
    {
        var json = await Ejecutar("listar-turnos-catalogo-grande.json");

        json["total"]!.GetValue<int>().Should().Be(60);
        json["mostrando"]!.GetValue<int>().Should().Be(ListarTurnosTool.MaximoTurnos);
        json["turnos"]!.AsArray().Should().HaveCount(ListarTurnosTool.MaximoTurnos);
        json["nota"]!.GetValue<string>().Should().Contain("50 de 60");
    }

    [Fact]
    public async Task ListarTurnos_FiltraSinAcentosNiMayusculas_CuandoRecibeFiltroNombre()
    {
        var json = await Ejecutar("listar-turnos.json", filtroNombre: "mañana");

        json["total"]!.GetValue<int>().Should().Be(1, "'mañana' debe encontrar 'MANANA'");
        json["turnos"]![0]!["nombre"]!.GetValue<string>().Should().Contain("MANANA");
    }
}
