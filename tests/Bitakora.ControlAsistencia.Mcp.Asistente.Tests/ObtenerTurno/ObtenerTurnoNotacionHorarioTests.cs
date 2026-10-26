using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ObtenerTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ObtenerTurno;

public class ObtenerTurnoNotacionHorarioTests
{
    private const string FranjaNoche =
        """{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}""";

    private static string Ficha(string franjas, bool esDescanso = false, string horarioResumido = "viejo") =>
        $$"""{"id":"id-turno","nombre":"Turno","esDescanso":{{(esDescanso ? "true" : "false")}},"horarioResumido":"{{horarioResumido}}","franjas":[{{franjas}}],"descripcion":"x","completo":true}""";

    private static async Task<JsonObject> Ejecutar(string ficha)
    {
        var (cliente, _) = ClienteFalso.Con(ficha);
        var tool = new ObtenerTurnoTool(new ProgramacionApi(cliente));
        var resultado = await tool.Run(null!, "id-turno", TestContext.Current.CancellationToken);
        return JsonNode.Parse(resultado)!.AsObject();
    }

    [Fact]
    public async Task ObtenerTurno_MuestraHorarioConNotacionUnica_CuandoNoche()
    {
        var json = await Ejecutar(Ficha(FranjaNoche));

        json["horario"]!.GetValue<string>().Should().Be("22:00-06:00+1");
    }

    [Fact]
    public async Task ObtenerTurno_MuestraHorarioConNotacionUnica_CuandoConDescanso()
    {
        var json = await Ejecutar(Ficha(
            """{"horaInicio":"08:00:00","horaFin":"17:00:00","diaOffsetFin":0,"descansos":[{"horaInicio":"13:00:00","horaFin":"14:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}"""));

        json["horario"]!.GetValue<string>().Should().Be("08:00-17:00, descanso 13:00-14:00");
    }

    [Fact]
    public async Task ObtenerTurno_MuestraHorarioConNotacionUnica_CuandoPartido()
    {
        var json = await Ejecutar(Ficha(
            """{"horaInicio":"06:00:00","horaFin":"10:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"},{"horaInicio":"14:00:00","horaFin":"18:00:00","diaOffsetFin":0,"descansos":[],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}"""));

        json["horario"]!.GetValue<string>().Should().Be("06:00-10:00, 14:00-18:00");
    }

    [Fact]
    public async Task ObtenerTurno_MuestraHorarioConNotacionUnica_CuandoDescansoPostMedianoche()
    {
        var json = await Ejecutar(Ficha(
            """{"horaInicio":"22:00:00","horaFin":"06:00:00","diaOffsetFin":1,"descansos":[{"horaInicio":"02:00:00","horaFin":"02:30:00","diaOffsetInicio":1,"diaOffsetFin":1}],"extras":[],"sedeId":null,"nombreSede":null,"descripcion":"x"}"""));

        json["horario"]!.GetValue<string>().Should().Be("22:00-06:00+1, descanso 02:00+1-02:30+1");
    }

    [Fact]
    public async Task ObtenerTurno_MuestraHorarioConNotacionUnica_CuandoConExtra()
    {
        var json = await Ejecutar(Ficha(
            """{"horaInicio":"08:00:00","horaFin":"16:00:00","diaOffsetFin":0,"descansos":[],"extras":[{"horaInicio":"16:00:00","horaFin":"18:00:00","diaOffsetInicio":0,"diaOffsetFin":0}],"sedeId":null,"nombreSede":null,"descripcion":"x"}"""));

        json["horario"]!.GetValue<string>().Should().Be("08:00-16:00, extra 16:00-18:00");
    }

    [Fact]
    public async Task ObtenerTurno_ConservaFranjasConSede_CuandoLaNotacionDelHorarioCambia()
    {
        var json = await Ejecutar(Ficha(FranjaNoche));

        json["franjas"]!.AsArray().Select(f => f!.GetValue<string>()).Should().Equal("22:00-06:00+1");
    }

    [Fact]
    public async Task ObtenerTurno_ConservaElHorarioResumido_CuandoElTurnoEsDescanso()
    {
        var json = await Ejecutar(Ficha(franjas: "", esDescanso: true, horarioResumido: "Descanso"));

        json["horario"]!.GetValue<string>().Should().Be("Descanso");
    }
}
