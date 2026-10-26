using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurnoDeHorarioAbierto;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearTurnoDeHorarioAbierto;

public class CrearTurnoDeHorarioAbiertoToolTests
{
    private const string Ruta = "/api/programacion/turnos/horario-abierto";

    private static (CrearTurnoDeHorarioAbiertoTool Tool, HandlerEnlatado Handler) CrearTool(
        HttpStatusCode status = HttpStatusCode.Created, string cuerpo = "")
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo, status);
        return (new CrearTurnoDeHorarioAbiertoTool(new ProgramacionApi(cliente)), handler);
    }

    private static Task<string> Ejecutar(CrearTurnoDeHorarioAbiertoTool tool, string nombre, string duracion) =>
        tool.Run(null!, nombre, duracion, TestContext.Current.CancellationToken);

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_EnviaElPostConGuidV7YRespondeEcoConVentana_CuandoLaDuracionEsSeisHoras()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, "Turno 6 horas", "6:00");

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be(Ruta);
        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["nombre"]!.GetValue<string>().Should().Be("Turno 6 horas");
        body["duracion"]!["horas"]!.GetValue<int>().Should().Be(6);
        body["duracion"]!["minutos"]!.GetValue<int>().Should().Be(0);
        var turnoId = body["turnoId"]!.GetValue<string>();
        Guid.TryParse(turnoId, out var guid).Should().BeTrue();
        guid.Version.Should().Be(7);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CrearTurnoDeHorarioAbiertoTool.Mensajes.ResultadoTurnoCreado);
        json["turno"]!["id"]!.GetValue<string>().Should().Be(turnoId);
        json["turno"]!["nombre"]!.GetValue<string>().Should().Be("Turno 6 horas");
        json["turno"]!["duracion"]!.GetValue<string>().Should().Be("6:00");
        json["turno"]!["franja"]!.GetValue<string>().Should().Be("04:00-10:00");
        json["turno"]!["entrada"]!.GetValue<string>().Should().Be("entre 04:00 y 18:00");
        json["nota"]!.GetValue<string>().Should().Be(CrearTurnoDeHorarioAbiertoTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_DiceTurnoFijoYFranjaHastaMedianoche_CuandoLaDuracionEsVeinteHoras()
    {
        var (tool, _) = CrearTool();

        var resultado = await Ejecutar(tool, "Turno largo", "20:00");

        var turno = JsonNode.Parse(resultado)!["turno"]!;
        turno["franja"]!.GetValue<string>().Should().Be("04:00-00:00");
        turno["entrada"]!.GetValue<string>().Should().Be("turno fijo (entrada a las 04:00)");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_EnviaYDescribeLosMinutos_CuandoLaDuracionNoEsHoraExacta()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, "Turno 6 y media", "6:30");

        JsonNode.Parse(handler.UltimoCuerpoEnviado!)!["duracion"]!["minutos"]!.GetValue<int>().Should().Be(30);
        var turno = JsonNode.Parse(resultado)!["turno"]!;
        turno["duracion"]!.GetValue<string>().Should().Be("6:30");
        turno["franja"]!.GetValue<string>().Should().Be("04:00-10:30");
        turno["entrada"]!.GetValue<string>().Should().Be("entre 04:00 y 17:30");
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_RespondeCampoObligatorioSinLlamar_CuandoElNombreEstaEnBlanco()
    {
        var (tool, handler) = CrearTool();

        var resultado = await Ejecutar(tool, "   ", "6:00");

        resultado.Should().Be(string.Format(CrearTurnoDeHorarioAbiertoTool.Mensajes.CampoObligatorio, "nombre"));
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_RespondeDuracionInvalidaSinLlamar_CuandoNoEsHmm()
    {
        foreach (var duracion in new[] { "6", "6h", "6:5", "-1:00" })
        {
            var (tool, handler) = CrearTool();

            var resultado = await Ejecutar(tool, "Turno", duracion);

            resultado.Should().Be(string.Format(CrearTurnoDeHorarioAbiertoTool.Mensajes.DuracionInvalida, duracion));
            handler.UltimaRequest.Should().BeNull();
        }
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe un turno con ese nombre";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await Ejecutar(tool, "Turno", "6:00");

        resultado.Should().Be(string.Format(CrearTurnoDeHorarioAbiertoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task CrearTurnoDeHorarioAbierto_TraduceElRechazoDelDominio_Cuando400PorDuracionFueraDeRango()
    {
        const string cuerpo = "La duracion debe ser mayor que 0:00 y hasta 20:00";
        foreach (var duracion in new[] { "0:00", "20:30" })
        {
            var (tool, _) = CrearTool(HttpStatusCode.BadRequest, cuerpo);

            var resultado = await Ejecutar(tool, "Turno", duracion);

            resultado.Should().Be(string.Format(CrearTurnoDeHorarioAbiertoTool.Mensajes.RechazoDelDominio, cuerpo));
        }
    }
}
