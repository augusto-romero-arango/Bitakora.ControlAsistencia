using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarTurnos;

public class ListarTurnosToolTests
{
    private const string Cursor = "eyJOb21icmUiOiJBIiwiSWQiOiIxIn0";

    private static ListarTurnosTool Tool(HttpClient cliente) =>
        new(new ProgramacionApi(cliente), new ResolutorSedePorCodigo(new SedesApi(cliente)));

    private static string ConCursor(string sobre, string cursor)
    {
        var json = JsonNode.Parse(sobre)!.AsObject();
        json["siguienteCursor"] = cursor;
        return json.ToJsonString();
    }

    private static async Task<(JsonNode Json, HandlerEnlatado Handler)> Ejecutar(
        string cuerpo, string? sede = null, string? cursor = null)
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo);
        var resultado = await Tool(cliente).Run(null!, sede, cursor, TestContext.Current.CancellationToken);
        return (JsonNode.Parse(resultado)!, handler);
    }

    [Fact]
    public async Task ListarTurnos_RemodelaCadaTurnoAIdNombreYHorario_CuandoElCatalogoResponde()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarTurnos", "listar-turnos.json"));

        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("total").Should().BeFalse("CA-ADR-0039: sobre sin total");
        json.AsObject().ContainsKey("nota").Should().BeFalse();
        json.AsObject().ContainsKey("siguienteCursor").Should().BeFalse();

        var primero = json["turnos"]![0]!.AsObject();
        primero["id"]!.GetValue<string>().Should().Be("01a04fd9-8876-77e1-9aa8-8853dc72191e");
        primero["nombre"]!.GetValue<string>().Should()
            .Be("[TEST]  limpieza   MANANA 01a04fd9-811b-7c71-be0c-fb5b28ce62b1");
        primero["horario"]!.GetValue<string>().Should().Be("08:00-16:00");
        primero.ContainsKey("franjas").Should().BeFalse();
        primero.ContainsKey("esDescanso").Should().BeFalse("esDescanso solo viaja cuando es true");
        primero.ContainsKey("enConstruccion").Should().BeFalse();
    }

    [Fact]
    public async Task ListarTurnos_MarcaElDescanso_CuandoElTurnoEsDescanso()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarTurnos", "listar-turnos.json"));

        json["turnos"]![2]!["esDescanso"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task ListarTurnos_MarcaEnConstruccion_CuandoElTurnoEsIncompleto()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarTurnos", "listar-turnos-con-turno-incompleto.json"));

        json["turnos"]![1]!["enConstruccion"]!.GetValue<bool>().Should().BeTrue();
        json["turnos"]![0]!.AsObject().ContainsKey("enConstruccion").Should().BeFalse();
    }

    [Fact]
    public async Task ListarTurnos_PideTakeIgualAlTopeSinCursor_CuandoNoRecibeParametros()
    {
        var (_, handler) = await Ejecutar(Fixtures.Leer("ListarTurnos", "listar-turnos.json"));

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/programacion/turnos");
        var query = Uri.UnescapeDataString(handler.UltimaRequest.RequestUri.Query);
        query.Should().Contain("take=50", "tope fijo, nunca 51 (CA-ADR-0039 decision 14)");
        query.Should().NotContain("cursor").And.NotContain("sedeId");
        ListarTurnosTool.MaximoTurnos.Should().Be(50);
    }

    [Fact]
    public async Task ListarTurnos_ReenviaElCursorTalCual_CuandoRecibeCursor()
    {
        var (_, handler) = await Ejecutar(Fixtures.Leer("ListarTurnos", "listar-turnos.json"), cursor: Cursor);

        Uri.UnescapeDataString(handler.UltimaRequest!.RequestUri!.Query).Should().Contain($"cursor={Cursor}");
    }

    [Fact]
    public async Task ListarTurnos_TraeSiguienteCursorYNota_CuandoElBackendTienePaginaSiguiente()
    {
        var cuerpo = ConCursor(Fixtures.Leer("ListarTurnos", "listar-turnos.json"), Cursor);

        var (json, _) = await Ejecutar(cuerpo);

        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json["turnos"]!.AsArray().Should().HaveCount(4, "la tool nunca descarta un elemento recibido");
        json["siguienteCursor"]!.GetValue<string>().Should().Be(Cursor);
        json["nota"]!.GetValue<string>().Should().Be(string.Format(ListarTurnosTool.Mensajes.NotaHayMasTurnos, Cursor));
        json.AsObject().ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ListarTurnos_EnviaElSedeIdResuelto_CuandoRecibeSede()
    {
        string? consultaTurnos = null;
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, "/api/sedes/fichas/NORTE", HttpStatusCode.OK,
            """{"id":"s:NORTE","codigo":"NORTE","nombre":"Norte","ciudad":null,"direccion":null,"centroDeCostos":null,"activa":false,"dispositivos":[]}""");
        handler.Responde(HttpMethod.Get, "/api/programacion/turnos", (request, _) =>
        {
            consultaTurnos = Uri.UnescapeDataString(request.RequestUri!.Query);
            return ClienteFalso.JsonOk(Fixtures.Leer("ListarTurnos", "listar-turnos.json"));
        });

        var resultado = await Tool(cliente).Run(null!, "NORTE", null, TestContext.Current.CancellationToken);

        consultaTurnos.Should().Contain("sedeId=s:NORTE", "no exige que la sede este activa");
        consultaTurnos.Should().Contain("take=50");
        JsonNode.Parse(resultado)!["turnos"]!.AsArray().Should().HaveCount(4);
    }

    [Fact]
    public async Task ListarTurnos_RemiteABuscarSedesSinLlamarAlCatalogo_CuandoLaSedeNoExiste()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, "/api/sedes/fichas/NOEXISTE", HttpStatusCode.NotFound);

        var resultado = await Tool(cliente).Run(null!, "NOEXISTE", null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarTurnosTool.Mensajes.SedeNoExiste, "NOEXISTE"));
        resultado.Should().Contain("buscar_sedes");
        handler.Requests.Should().NotContain(r => r.Ruta == "/api/programacion/turnos");
    }

    [Fact]
    public async Task ListarTurnos_RespondeFraseDelCatalogo_CuandoNoHayTurnos()
    {
        var (cliente, _) = ClienteFalso.Con("""{"elementos":[]}""");

        var resultado = await Tool(cliente).Run(null!, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(ListarTurnosTool.Mensajes.SinTurnos);
    }

    [Fact]
    public async Task ListarTurnos_RespondeFraseConLaSede_CuandoNoHayTurnosEnLaSede()
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, "/api/sedes/fichas/NORTE", HttpStatusCode.OK,
            """{"id":"s:NORTE","codigo":"NORTE","nombre":"Norte","ciudad":null,"direccion":null,"centroDeCostos":null,"activa":true,"dispositivos":[]}""");
        handler.Responde(HttpMethod.Get, "/api/programacion/turnos", HttpStatusCode.OK, """{"elementos":[]}""");

        var resultado = await Tool(cliente).Run(null!, "NORTE", null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarTurnosTool.Mensajes.SinTurnosEnSede, "NORTE"));
    }

    [Fact]
    public async Task ListarTurnos_RespondeTextoDeRechazo_CuandoElBackendResponde400PorCursorInvalido()
    {
        var (cliente, _) = ClienteFalso.Con("El cursor es invalido", HttpStatusCode.BadRequest);

        var resultado = await Tool(cliente).Run(null!, null, "basura", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarTurnosTool.Mensajes.RechazoDelDominio, "El cursor es invalido"));
    }

    [Fact]
    public async Task ListarTurnos_RespondeTextoDeRechazo_CuandoElBackendResponde5xx()
    {
        var (cliente, _) = ClienteFalso.Con("", HttpStatusCode.ServiceUnavailable);

        var resultado = await Tool(cliente).Run(null!, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarTurnosTool.Mensajes.RechazoDelDominio, "503"));
    }
}
