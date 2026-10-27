using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.RetirarTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.RetirarTurno;

public class RetirarTurnoToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string TurnoIdCocinaManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoInexistenteId = "8f14e45f-ceea-4b3c-8f0a-0000000000ff";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(RetirarTurnoTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        if (statusDetalle == HttpStatusCode.OK)
            handler.RespondeDetalleDeTurno(turnosJson ?? TurnosJson, TurnoIdCocinaManana);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana), statusDetalle, "");
        handler.Responde(HttpMethod.Delete, $"{RutaTurnos}/{TurnoIdCocinaManana}", statusDelete, cuerpoDelete);

        var programacionApi = new ProgramacionApi(cliente);
        var tool = new RetirarTurnoTool(programacionApi, new ResolutorTurnoPorId(programacionApi));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(Fakes fakes, string turnoId = TurnoIdCocinaManana) =>
        fakes.Tool.Run(context: null!, turnoId: turnoId, ct: TestContext.Current.CancellationToken);

    // CA-4: lee el detalle por id, envia el DELETE a ese id y el eco conserva el nombre leido.
    [Fact]
    public async Task RetirarTurno_EnviaElDeleteAlIdYEcoConElNombreDelDetalle_CuandoElTurnoExiste()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Delete && r.Ruta == $"{RutaTurnos}/{TurnoIdCocinaManana}");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(RetirarTurnoTool.Mensajes.ResultadoTurnoRetirado);
        json["turno"]!["id"]!.GetValue<string>().Should().Be(TurnoIdCocinaManana);
        json["turno"]!["nombre"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["nota"]!.GetValue<string>().Should().Be(RetirarTurnoTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-5: nunca llama al catalogo completo.
    [Fact]
    public async Task RetirarTurno_LeeElDetalleDelTurnoPorId_SinConsultarElCatalogo()
    {
        var fakes = CrearTool();

        await Ejecutar(fakes);

        fakes.Handler.Requests.Should().Contain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana));
        fakes.Handler.Requests.Should().NotContain(r =>
            r.Metodo == HttpMethod.Get && r.Ruta == DetalleDeTurnoFalso.RutaCatalogo);
    }

    // CA-3: detalle 404 -> frase con el id que remite a buscar_turnos, sin DELETE.
    [Fact]
    public async Task RetirarTurno_RespondeTurnoNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.TurnoNoExiste, TurnoIdCocinaManana));
        resultado.Should().Contain(TurnoIdCocinaManana).And.Contain("buscar_turnos").And.NotContain("disponibles");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    // CA-2: turno_id que no es GUID -> mensaje del .resx con el valor, sin backend.
    [Fact]
    public async Task RetirarTurno_RechazaSinLlamarAlBackend_CuandoTurnoIdNoEsUnGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, turnoId: "Cocina Manana");

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.TurnoIdInvalido, "Cocina Manana"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirarTurno_RechazaSinLlamarAlBackend_CuandoTurnoIdEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, turnoId: "   ");

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.TurnoIdInvalido, "   "));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task RetirarTurno_TraduceElRechazoDelDominio_Cuando404DelDelete()
    {
        const string cuerpo = "El turno no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarTurno_TraduceElRechazoDelDominio_Cuando409DelDelete()
    {
        const string cuerpo = "El turno no puede retirarse en este momento";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RetirarTurno_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(RetirarTurnoTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
