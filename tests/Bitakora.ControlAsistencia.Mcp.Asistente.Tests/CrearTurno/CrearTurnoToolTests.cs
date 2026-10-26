using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.CrearTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.CrearTurno;

public class CrearTurnoToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";

    private static string Validacion400Json => Fixtures.Leer("CrearTurno", "validacion-400.json");

    private static (CrearTurnoTool Tool, HandlerEnlatado Handler) CrearTool(
        HttpStatusCode status = HttpStatusCode.Created, string cuerpo = "")
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo, status);
        return (new CrearTurnoTool(new ProgramacionApi(cliente), new ResolutorSedePorCodigo(new SedesApi(ClienteFalso.Con("").Cliente))), handler);
    }

    // CA-1: sin es_descanso, el body envia esDescanso:false y el eco responde completo:false --
    // un turno recien creado sin franjas no es programable.
    [Fact]
    public async Task CrearTurno_EnviaElBodyConGuidV7YEsDescansoFalso_YDevuelveElEcoIncompleto_CuandoEsDescansoNoSeEnvia()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "Nocturno", false, null, TestContext.Current.CancellationToken);

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
            null!, "Descanso semanal", true, null, TestContext.Current.CancellationToken);

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

        var resultado = await tool.Run(null!, "Nocturno", null, null, TestContext.Current.CancellationToken);

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

        var resultado = await tool.Run(null!, "Nocturno", false, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task CrearTurno_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var (tool, _) = CrearTool(HttpStatusCode.BadRequest, fixture);

        var resultado = await tool.Run(null!, "Nocturno", false, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task CrearTurno_RechazaSinLlamarAlDominio_CuandoElNombreEstaEnBlanco()
    {
        var (tool, handler) = CrearTool();

        var resultado = await tool.Run(null!, "   ", false, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.CampoObligatorio, "nombre"));
        handler.UltimaRequest.Should().BeNull("un nombre en blanco no debe llegar al dominio");
    }

    private static string SedeJson => Fixtures.Leer("AgregarFranja", "sede.json");
    private static string SedeInactivaJson => Fixtures.Leer("AgregarFranja", "sede-inactiva.json");

    private sealed record ToolConSedes(CrearTurnoTool Tool, HandlerEnlatado Programacion, HandlerFuncional Sedes);

    private static ToolConSedes CrearToolConSedes(
        HttpStatusCode statusCrear = HttpStatusCode.Created,
        string cuerpoCrear = "",
        string? sedeJson = null,
        HttpStatusCode statusSede = HttpStatusCode.OK)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.Con(cuerpoCrear, statusCrear);
        var json = sedeJson ?? SedeJson;
        var (clienteSedes, handlerSedes) = ClienteFalso.ConFuncion(_ =>
            new HttpResponseMessage(statusSede)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        var tool = new CrearTurnoTool(
            new ProgramacionApi(clienteProgramacion), new ResolutorSedePorCodigo(new SedesApi(clienteSedes)));
        return new ToolConSedes(tool, handlerProgramacion, handlerSedes);
    }

    private static Task<string> EjecutarConFranjas(CrearTurnoTool tool, string? franjas, bool? esDescanso = null) =>
        tool.Run(null!, "Turno completo", esDescanso, franjas, TestContext.Current.CancellationToken);

    // CA-1: las ordinarias viajan en el mismo POST; sin inicio == fin no hay diaOffsetFin.
    [Fact]
    public async Task CrearTurno_EnviaLasOrdinariasEnElMismoPost_CuandoLlegaFranjasValido()
    {
        var fakes = CrearToolConSedes();

        await EjecutarConFranjas(
            fakes.Tool,
            """[{"franja":"06:00-14:00","descansos":["10:00-10:15"],"extras":["13:00-14:00"],"codigo_sede":"SUBA"}]""");

        var body = JsonNode.Parse(fakes.Programacion.UltimoCuerpoEnviado!)!;
        body["nombre"]!.GetValue<string>().Should().Be("Turno completo");
        body["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        var ordinaria = body["ordinarias"]!.AsArray().Single()!;
        ordinaria["inicio"]!.GetValue<string>().Should().Be("06:00");
        ordinaria["fin"]!.GetValue<string>().Should().Be("14:00");
        ordinaria.AsObject().ContainsKey("diaOffsetFin").Should().BeFalse();
        ordinaria["descansos"]!.AsArray().Single()!["inicio"]!.GetValue<string>().Should().Be("10:00");
        ordinaria["descansos"]!.AsArray().Single()!["fin"]!.GetValue<string>().Should().Be("10:15");
        ordinaria["extras"]!.AsArray().Single()!["inicio"]!.GetValue<string>().Should().Be("13:00");
        ordinaria["extras"]!.AsArray().Single()!["fin"]!.GetValue<string>().Should().Be("14:00");
        ordinaria["sede"]!["id"]!.GetValue<string>().Should().Be("SUBA");
        ordinaria["sede"]!["nombre"]!.GetValue<string>().Should().Be("Suba");
    }

    [Fact]
    public async Task CrearTurno_EnviaDiaOffsetFinUnoSoloEnLaFranjaDe24Horas_CuandoInicioYFinSonIguales()
    {
        var fakes = CrearToolConSedes();

        await EjecutarConFranjas(fakes.Tool, """[{"franja":"08:00-08:00"},{"franja":"22:00-06:00"}]""");

        var ordinarias = JsonNode.Parse(fakes.Programacion.UltimoCuerpoEnviado!)!["ordinarias"]!.AsArray();
        ordinarias[0]!["diaOffsetFin"]!.GetValue<int>().Should().Be(1);
        ordinarias[1]!.AsObject().ContainsKey("diaOffsetFin").Should().BeFalse();
    }

    [Fact]
    public async Task CrearTurno_OmiteOrdinariasDelBody_CuandoNoLlegaFranjas()
    {
        var fakes = CrearToolConSedes();

        await EjecutarConFranjas(fakes.Tool, null);

        JsonNode.Parse(fakes.Programacion.UltimoCuerpoEnviado!)!.AsObject()
            .ContainsKey("ordinarias").Should().BeFalse();
    }

    // CA-2: eco completo con franjas en NotacionFranja.
    [Fact]
    public async Task CrearTurno_DevuelveElEcoCompletoConLasFranjasCompactadas_CuandoLlegaFranjasValido()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(
            fakes.Tool,
            """[{"franja":"22:00-06:00","descansos":["02:00-02:30"],"codigo_sede":"SUBA"},{"franja":"08:00-08:00"}]""");

        var turno = JsonNode.Parse(resultado)!["turno"]!;
        turno["completo"]!.GetValue<bool>().Should().BeTrue();
        turno["esDescanso"]!.GetValue<bool>().Should().BeFalse();
        turno["franjas"]!.AsArray().Select(f => f!.GetValue<string>()).Should().Equal(
            "22:00-06:00+1, descanso 02:00-02:30, sede: Suba",
            "08:00-08:00+1");
    }

    [Fact]
    public async Task CrearTurno_ConservaElEcoDeSiempre_CuandoNoLlegaFranjas()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(fakes.Tool, null);

        var turno = JsonNode.Parse(resultado)!["turno"]!;
        turno["completo"]!.GetValue<bool>().Should().BeFalse();
        turno.AsObject().ContainsKey("franjas").Should().BeFalse();
    }

    // CA-3: validacion previa, sin tocar ningun dominio.
    [Fact]
    public async Task CrearTurno_RespondeFranjasJsonInvalido_CuandoFranjasNoEsJson()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(fakes.Tool, "06:00-14:00");

        resultado.Should().Be(CrearTurnoTool.Mensajes.FranjasJsonInvalido);
        fakes.Programacion.UltimaRequest.Should().BeNull();
        fakes.Sedes.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CrearTurno_RespondeFranjaObligatoria_CuandoUnElementoNoTraeFranja()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(
            fakes.Tool, """[{"franja":"06:00-14:00"},{"descansos":["10:00-10:15"]}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.FranjaObligatoria, 2));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurno_RespondeFranjaInvalida_CuandoLaFranjaNoTieneFormatoHoraRango()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(fakes.Tool, """[{"franja":"6am-2pm"}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.FranjaInvalida, 1, "6am-2pm"));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurno_RespondeDescansoInvalido_CuandoUnDescansoNoTieneFormatoHoraRango()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(
            fakes.Tool, """[{"franja":"06:00-14:00","descansos":["10:00"]}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.DescansoInvalido, 1, "10:00"));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurno_RespondeExtraInvalido_CuandoUnExtraNoTieneFormatoHoraRango()
    {
        var fakes = CrearToolConSedes();

        var resultado = await EjecutarConFranjas(
            fakes.Tool, """[{"franja":"06:00-14:00","extras":["13:00-xx"]}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.ExtraInvalido, 1, "13:00-xx"));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    // CA-4
    [Fact]
    public async Task CrearTurno_RespondeSedeNoExisteSinPost_CuandoElCodigoNoExiste()
    {
        var fakes = CrearToolConSedes(sedeJson: "", statusSede: HttpStatusCode.NotFound);

        var resultado = await EjecutarConFranjas(
            fakes.Tool, """[{"franja":"06:00-14:00","codigo_sede":"NORTE"}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.SedeNoExiste, "NORTE"));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurno_RespondeSedeInactivaSinPost_CuandoLaSedeNoEstaActiva()
    {
        var fakes = CrearToolConSedes(sedeJson: SedeInactivaJson);

        var resultado = await EjecutarConFranjas(
            fakes.Tool, """[{"franja":"06:00-14:00","codigo_sede":"SUBA"}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.SedeInactiva, "SUBA"));
        fakes.Programacion.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CrearTurno_ResuelveCadaCodigoDeSedeUnaSolaVez_CuandoSeRepiteEnVariasFranjas()
    {
        var fakes = CrearToolConSedes();

        await EjecutarConFranjas(
            fakes.Tool,
            """[{"franja":"06:00-10:00","codigo_sede":"SUBA"},{"franja":"12:00-16:00","codigo_sede":"SUBA"}]""");

        fakes.Sedes.Requests.Should().ContainSingle();
    }

    // CA-5
    [Fact]
    public async Task CrearTurno_TraduceElRechazoDelDominio_Cuando400ConFranjas()
    {
        var fixture = Validacion400Json;
        var fakes = CrearToolConSedes(HttpStatusCode.BadRequest, fixture);

        var resultado = await EjecutarConFranjas(fakes.Tool, """[{"franja":"06:00-14:00","descansos":["15:00-15:15"]}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task CrearTurno_TraduceElRechazoDelDominio_Cuando409ConFranjas()
    {
        const string cuerpo = "Ya existe un turno con el nombre 'Turno completo'";
        var fakes = CrearToolConSedes(HttpStatusCode.Conflict, cuerpo);

        var resultado = await EjecutarConFranjas(fakes.Tool, """[{"franja":"06:00-14:00"}]""");

        resultado.Should().Be(string.Format(CrearTurnoTool.Mensajes.RechazoDelDominio, cuerpo));
    }
}
