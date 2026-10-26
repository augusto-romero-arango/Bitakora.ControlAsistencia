using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarTurnos;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarTurnos;

public class BuscarTurnosNotacionHorarioTests
{
    private const string Catalogo = """{"elementos":[{"id":"id-Noche","nombre":"Noche","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-ConDescanso","nombre":"ConDescanso","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"08:00:00","horaFin":"17:00:00","diaOffsetFin":0,"descansos":[{"horaInicio":"13:00:00","horaFin":"14:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-Partido","nombre":"Partido","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"06:00:00","horaFin":"10:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"},{"horaInicio":"14:00:00","horaFin":"18:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-DescansoPostMedianoche","nombre":"DescansoPostMedianoche","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[{"horaInicio":"02:00:00","horaFin":"02:30:00","diaOffsetInicio":1,"diaOffsetFin":1}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-ConExtra","nombre":"ConExtra","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"08:00:00","horaFin":"16:00:00","diaOffsetFin":0,"descansos":[],"extras":[{"horaInicio":"16:00:00","horaFin":"18:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true}]}""";

    private static async Task<string> HorarioDe(string nombre)
    {
        var (cliente, _) = ClienteFalso.Con(Catalogo);
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
