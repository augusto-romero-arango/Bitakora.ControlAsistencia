using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.RetirarTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.RetirarTurno;

public class RetirarTurnoToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string TurnoIdCocinaManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";

    // Reuso deliberado del fixture de solicitar_programacion_turno (CA-4): mismo catalogo, mismo
    // resolutor por nombre.
    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(RetirarTurnoTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.Accepted,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosJson);
        handler.Responde(HttpMethod.Delete, $"{RutaTurnos}/{TurnoIdCocinaManana}", statusDelete, cuerpoDelete);

        var tool = new RetirarTurnoTool(new ProgramacionApi(cliente));
        return new Fakes(tool, handler);
    }

    // CA-3: resuelve "Cocina Manana" en el catalogo y envia el DELETE al id correspondiente.
    [Fact]
    public async Task RetirarTurno_ResuelveElIdPorNombreYEnviaElDelete_CuandoElTurnoExiste()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Cocina Manana", TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Delete && r.Ruta == $"{RutaTurnos}/{TurnoIdCocinaManana}");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(RetirarTurnoTool.Mensajes.ResultadoTurnoRetirado);
        json["turno"]!["id"]!.GetValue<string>().Should().Be(TurnoIdCocinaManana);
        json["turno"]!["nombre"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["nota"]!.GetValue<string>().Should().Be(RetirarTurnoTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-3: coincidencia exacta bajo trim + colapso de espacios + case-insensitive, mismo criterio
    // que solicitar_programacion_turno (resolutor compartido, CA-4).
    [Fact]
    public async Task RetirarTurno_ResuelveElTurno_ConTrimColapsoDeEspaciosYCaseInsensitive()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "  cocina   manana  ", TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["turno"]!["nombre"]!.GetValue<string>().Should().Be("Cocina Manana");
    }

    [Fact]
    public async Task RetirarTurno_RespondeTurnoNoExisteSinDelete_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(
            null!, "Turno Que No Existe", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            RetirarTurnoTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Cocina Manana, Cocina Tarde"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task RetirarTurno_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "El turno no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await fakes.Tool.Run(
            null!, "Cocina Manana", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarTurno_TraduceElRechazoDelDominio_Cuando409DelDelete()
    {
        const string cuerpo = "El turno ya fue retirado";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await fakes.Tool.Run(
            null!, "Cocina Manana", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarTurno_RechazaSinLlamarAlDominio_CuandoElTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await fakes.Tool.Run(null!, "   ", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.CampoObligatorio, "turno"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    // Boundary del sistema: un fallo del catalogo se traduce a texto y corta antes del DELETE
    // (CA-ADR-0030), mismo criterio que solicitar_programacion_turno.
    [Fact]
    public async Task RetirarTurno_RespondeElRechazoDelDominioSinDelete_CuandoElCatalogoFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await fakes.Tool.Run(
            null!, "Cocina Manana", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
