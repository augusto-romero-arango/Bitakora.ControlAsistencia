using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarTurnos;

public class BuscarTurnosNotacionHorarioTests
{
    private static async Task<string> HorarioDe(string nombre)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("BuscarTurnos", "buscar-turnos-notacion-horario.json"));
        var tool = new BuscarTurnosTool(new ProgramacionApi(cliente));
        var resultado = await tool.Run(null!, nombre, null, null, TestContext.Current.CancellationToken);
        return JsonNode.Parse(resultado)!["turnos"]![0]!["horario"]!.GetValue<string>();
    }

    [Fact]
    public async Task BuscarTurnos_MuestraHorarioConNotacionUnica_CuandoNoche()
    {
        var horario = await HorarioDe("Noche");

        horario.Should().Be("22:00-06:00+1");
    }

    [Fact]
    public async Task BuscarTurnos_MuestraHorarioConNotacionUnica_CuandoConDescanso()
    {
        var horario = await HorarioDe("ConDescanso");

        horario.Should().Be("08:00-17:00, descanso 13:00-14:00");
    }

    [Fact]
    public async Task BuscarTurnos_MuestraHorarioConNotacionUnica_CuandoPartido()
    {
        var horario = await HorarioDe("Partido");

        horario.Should().Be("06:00-10:00, 14:00-18:00");
    }

    [Fact]
    public async Task BuscarTurnos_MuestraHorarioConNotacionUnica_CuandoDescansoPostMedianoche()
    {
        var horario = await HorarioDe("DescansoPostMedianoche");

        horario.Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public async Task BuscarTurnos_MuestraHorarioConNotacionUnica_CuandoConExtra()
    {
        var horario = await HorarioDe("ConExtra");

        horario.Should().Be("08:00-16:00, extra 16:00-18:00");
    }
}
