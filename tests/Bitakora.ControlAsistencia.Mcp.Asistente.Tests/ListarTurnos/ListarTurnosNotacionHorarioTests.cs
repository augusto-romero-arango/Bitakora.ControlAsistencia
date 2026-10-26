using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarTurnos;

public class ListarTurnosNotacionHorarioTests
{
    private const string Catalogo = """{"elementos":[{"id":"id-Noche","nombre":"Noche","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-ConDescanso","nombre":"ConDescanso","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"08:00:00","horaFin":"17:00:00","diaOffsetFin":0,"descansos":[{"horaInicio":"13:00:00","horaFin":"14:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-Partido","nombre":"Partido","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"06:00:00","horaFin":"10:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"},{"horaInicio":"14:00:00","horaFin":"18:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-DescansoPostMedianoche","nombre":"DescansoPostMedianoche","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[{"horaInicio":"02:00:00","horaFin":"02:30:00","diaOffsetInicio":1,"diaOffsetFin":1}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true},{"id":"id-ConExtra","nombre":"ConExtra","esDescanso":false,"horarioResumido":"viejo","franjas":[{"horaInicio":"08:00:00","horaFin":"16:00:00","diaOffsetFin":0,"descansos":[],"extras":[{"horaInicio":"16:00:00","horaFin":"18:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"sedeId":null,"nombreSede":null,"descripcion":"x"}],"descripcion":"x","completo":true}]}""";

    private static async Task<JsonNode> Ejecutar()
    {
        var (cliente, _) = ClienteFalso.Con(Catalogo);
        var tool = new ListarTurnosTool(new ProgramacionApi(cliente), new ResolutorSedePorCodigo(new SedesApi(cliente)));
        var resultado = await tool.Run(null!, null, null, TestContext.Current.CancellationToken);
        return JsonNode.Parse(resultado)!;
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoNoche()
    {
        var json = await Ejecutar();

        json["turnos"]![0]!["horario"]!.GetValue<string>().Should().Be("22:00-06:00+1");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoConDescanso()
    {
        var json = await Ejecutar();

        json["turnos"]![1]!["horario"]!.GetValue<string>().Should().Be("08:00-17:00, descanso 13:00-14:00");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoPartido()
    {
        var json = await Ejecutar();

        json["turnos"]![2]!["horario"]!.GetValue<string>().Should().Be("06:00-10:00, 14:00-18:00");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoDescansoPostMedianoche()
    {
        var json = await Ejecutar();

        json["turnos"]![3]!["horario"]!.GetValue<string>().Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public async Task ListarTurnos_MuestraHorarioConNotacionUnica_CuandoConExtra()
    {
        var json = await Ejecutar();

        json["turnos"]![4]!["horario"]!.GetValue<string>().Should().Be("08:00-16:00, extra 16:00-18:00");
    }
}
