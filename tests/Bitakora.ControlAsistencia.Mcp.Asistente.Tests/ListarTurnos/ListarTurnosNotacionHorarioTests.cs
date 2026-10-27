using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarTurnos;

public class ListarTurnosNotacionHorarioTests
{
    private static async Task<string> HorarioDe(string nombre)
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ListarTurnos", "listar-turnos-notacion-horario.json"));
        var tool = new ListarTurnosTool(new ProgramacionApi(cliente), new ResolutorSedePorCodigo(new SedesApi(cliente)));
        var resultado = await tool.Run(null!, null, null, TestContext.Current.CancellationToken);
        return JsonNode.Parse(resultado)!["turnos"]!.AsArray()
            .Single(t => t!["nombre"]!.GetValue<string>() == nombre)!["horario"]!.GetValue<string>();
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoNoche()
    {
        var horario = await HorarioDe("Noche");

        horario.Should().Be("22:00-06:00+1");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoConDescanso()
    {
        var horario = await HorarioDe("ConDescanso");

        horario.Should().Be("08:00-17:00, descanso 13:00-14:00");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoPartido()
    {
        var horario = await HorarioDe("Partido");

        horario.Should().Be("06:00-10:00, 14:00-18:00");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoDescansoPostMedianoche()
    {
        var horario = await HorarioDe("DescansoPostMedianoche");

        horario.Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoConExtra()
    {
        var horario = await HorarioDe("ConExtra");

        horario.Should().Be("08:00-16:00, extra 16:00-18:00");
    }

    [Fact]
    public async Task ListarTurnos_ConservaElHorarioResumido_CuandoElTurnoEsDescanso()
    {
        var horario = await HorarioDe("Descanso");

        horario.Should().Be("Descanso");
    }

    [Fact]
    public async Task ListarTurnos_ConservaElHorarioResumido_CuandoElTurnoEstaEnConstruccion()
    {
        var horario = await HorarioDe("EnConstruccion");

        horario.Should().Be("Sin franjas");
    }
}
