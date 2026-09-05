using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.CrearTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.CrearTurno;

public class CrearTurnoToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";

    private static string Validacion400Json => Fixtures.Leer("CrearTurno", "validacion-400.json");

    private static (CrearTurnoTool Tool, HandlerEnlatado Handler) CrearTool(
        HttpStatusCode status = HttpStatusCode.Accepted, string cuerpo = "")
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo, status);
        return (new CrearTurnoTool(new ProgramacionApi(cliente)), handler);
    }

    // CA-1: sin es_descanso, el body envia esDescanso:false y el eco responde completo:false --
    // un turno recien creado sin franjas no es programable.
    [Fact]
    public async Task CrearTurno_EnviaElBodyConGuidV7YEsDescansoFalso_YDevuelveElEcoIncompleto_CuandoEsDescansoNoSeEnvia()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "Nocturno", false, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be(RutaTurnos);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["nombre"]!.GetValue<string>().Should().Be("Nocturno");
        body["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        var turnoIdEnviado = body["turnoId"]!.GetValue<string>();
        Guid.TryParse(turnoIdEnviado, out _).Should().BeTrue("turnoId debe ser un Guid v7 valido");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CrearTurnoTool.Mensajes.ResultadoTurnoCreado);
        json["turno"]!["id"]!.GetValue<string>().Should().Be(turnoIdEnviado);
        json["turno"]!["nombre"]!.GetValue<string>().Should().Be("Nocturno");
        json["turno"]!["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        json["turno"]!["completo"]!.GetValue<bool>().Should()
            .BeFalse("un turno recien creado sin franjas no se puede programar");
        json["nota"]!.GetValue<string>().Should().Be(CrearTurnoTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-1: con es_descanso true, el body envia esDescanso:true y el eco responde completo:true --
    // un descanso nace completo, no necesita franjas.
    [Fact]
    public async Task CrearTurno_EnviaEsDescansoTrue_YDevuelveElEcoCompleto_CuandoEsDescansoEsTrue()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(
            null!, "Descanso semanal", true, TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["esDescanso"]!.GetValue<bool>().Should().BeTrue();

        var json = JsonNode.Parse(resultado)!;
        json["turno"]!["esDescanso"]!.GetValue<bool>().Should().BeTrue();
        json["turno"]!["completo"]!.GetValue<bool>().Should()
            .BeTrue("un descanso nace completo: no necesita franjas para poder programarse");
    }

    // El cliente MCP omite es_descanso: la extension deja el argumento sin resolver y la tool lo
    // recibe null. Debe comportarse igual que un false explicito, nunca reventar.
    [Fact]
    public async Task CrearTurno_EnviaEsDescansoFalso_CuandoElClienteOmiteEsDescanso()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "Nocturno", null, TestContext.Current.CancellationToken);

        JsonNode.Parse(handler.UltimoCuerpoEnviado!)!["esDescanso"]!.GetValue<bool>().Should().BeFalse();

        var json = JsonNode.Parse(resultado)!;
        json["turno"]!["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        json["turno"]!["completo"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task CrearTurno_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "Ya existe un turno con el nombre 'Nocturno'";
        var (tool, _) = CrearTool(HttpStatusCode.Conflict, cuerpo);

        var resultado = await tool.Run(null!, "Nocturno", false, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task CrearTurno_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var (tool, _) = CrearTool(HttpStatusCode.BadRequest, fixture);

        var resultado = await tool.Run(null!, "Nocturno", false, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task CrearTurno_RechazaSinLlamarAlDominio_CuandoElNombreEstaEnBlanco()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "   ", false, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.CampoObligatorio, "nombre"));
        handler.UltimaRequest.Should().BeNull("un nombre en blanco no debe llegar al dominio");
    }
}
