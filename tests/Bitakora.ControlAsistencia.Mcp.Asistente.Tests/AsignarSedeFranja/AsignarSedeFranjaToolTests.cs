using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarSedeFranja;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarSedeFranja;

public class AsignarSedeFranjaToolTests
{
    private const string Turno = "Partido";
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000020";
    private const string SedeCodigo = "CHAPINERO";
    private const string RutaCatalogo = "/api/programacion/turnos";
    private const string RutaTurno = $"/api/programacion/turnos/{TurnoId}";
    private const string RutaAsignarSedeFranja = $"/api/programacion/turnos/{TurnoId}:asignar-sede-franja";

    private static string TurnosJson => Fixtures.Leer("AsignarSedeFranja", "turno.json");
    private static string SedeJson => Fixtures.Leer("AsignarSedeFranja", "sede.json");
    private static string SedeInactivaJson => Fixtures.Leer("AsignarSedeFranja", "sede-inactiva.json");
    private static string Validacion400Json => Fixtures.Leer("AsignarSedeFranja", "validacion-400.json");

    private sealed record Fakes(AsignarSedeFranjaTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Sedes);

    // Composicion por defecto del camino feliz -- cada test sobreescribe unicamente el fixture que
    // le interesa via argumentos nombrados, mismo patron de consolidacion que las demas tools.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusAsignar = HttpStatusCode.NoContent,
        string cuerpoAsignar = "",
        string? sedeJson = null,
        HttpStatusCode statusSede = HttpStatusCode.OK)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurno, statusTurnos, turnosJson ?? TurnosJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaAsignarSedeFranja, statusAsignar, cuerpoAsignar);

        var (clienteSedes, handlerSedes) = ClienteFalso.Con(sedeJson ?? SedeJson, statusSede);

        var programacionApi = new ProgramacionApi(clienteProgramacion);
        var tool = new AsignarSedeFranjaTool(
            programacionApi, new ResolutorTurnoPorId(programacionApi), new ResolutorSedePorCodigo(new SedesApi(clienteSedes)));

        return new Fakes(tool, handlerProgramacion, handlerSedes);
    }

    private static Task<string> Ejecutar(
        AsignarSedeFranjaTool tool,
        string turnoId = TurnoId,
        string franja = "14:00",
        string? codigoSede = null,
        CancellationToken ct = default) =>
        tool.Run(context: null!, turnoId: turnoId, franja: franja, codigoSede: codigoSede, ct: ct);

    // CA-3: ningun rechazo local debe tocar ninguno de los 2 dominios.
    private static void AsegurarNingunaRequest(Fakes fakes)
    {
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Sedes.UltimaRequest.Should().BeNull();
    }

    // CA-1: con codigo_sede, la tool consulta Sedes, resuelve el turno y envia la sede resuelta.
    [Fact]
    public async Task AsignarSedeFranja_EnviaLaSedeResuelta_CuandoLlegaCodigoSede()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        fakes.Sedes.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/sedes/fichas/{SedeCodigo}");
        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["franja"]!.GetValue<string>().Should().Be("14:00");
        body["sede"]!["id"]!.GetValue<string>().Should().Be(SedeCodigo);
        body["sede"]!["nombre"]!.GetValue<string>().Should().Be("Chapinero");
        body["sede"]!["centroDeCostos"]!.GetValue<string>().Should().Be("CC-200");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarSedeFranjaTool.Mensajes.ResultadoSedeAsignada);
        json["turno"]!.GetValue<string>().Should().Be(Turno);
        json["franja"]!.GetValue<string>().Should().Be("14:00");
        json["sede"]!.GetValue<string>().Should().Be("Chapinero");
        json["nota"]!.GetValue<string>().Should().Be(AsignarSedeFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-2: sin codigo_sede, no se consulta Sedes y el body/eco viajan sin la clave sede.
    [Fact]
    public async Task AsignarSedeFranja_RetiraLaSedeSinConsultarSedes_CuandoNoLlegaCodigoSede()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Sedes.UltimaRequest.Should().BeNull("sin codigo_sede no debe consultarse Sedes");
        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["franja"]!.GetValue<string>().Should().Be("14:00");
        body.AsObject().ContainsKey("sede").Should().BeFalse();

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarSedeFranjaTool.Mensajes.ResultadoSedeRetirada);
        json["franja"]!.GetValue<string>().Should().Be("14:00");
        json.AsObject().ContainsKey("sede").Should().BeFalse();
        json["nota"]!.GetValue<string>().Should().Be(AsignarSedeFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task AsignarSedeFranja_RespondeSedeNoExisteSinPost_CuandoLaSedeNoExiste()
    {
        var fakes = CrearTool(sedeJson: "", statusSede: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, codigoSede: "NORTE", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.SedeNoExiste, "NORTE"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task AsignarSedeFranja_RespondeSedeInactivaSinPost_CuandoLaSedeNoEstaActiva()
    {
        var fakes = CrearTool(sedeJson: SedeInactivaJson);

        var resultado = await Ejecutar(fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.SedeInactiva, SedeCodigo));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task AsignarSedeFranja_RechazaSinLlamarANingunDominio_CuandoFranjaNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "2pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.HoraInvalida, "franja", "2pm"));
        AsegurarNingunaRequest(fakes);
    }

    // CA-3: el detalle por identidad responde 404 -> frase con el id que remite a buscar_turnos,
    // sin lista de nombres y sin invocar el comando.
    [Fact]
    public async Task AsignarSedeFranja_RespondeTurnoNoExisteSinPost_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.TurnoNoExiste, TurnoId));
        resultado.Should().Contain(TurnoId).And.Contain("buscar_turnos").And.NotContain("disponibles");
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    // CA-3: FranjaSinSede es el 409 propio del dominio al retirar una franja que ya no tiene sede
    // prearmada -- la tool solo lo traduce a texto, sin logica propia.
    [Fact]
    public async Task AsignarSedeFranja_TraduceElRechazoDelDominio_Cuando409PorFranjaSinSede()
    {
        const string cuerpo = "La franja no tiene sede prearmada";
        var fakes = CrearTool(statusAsignar: HttpStatusCode.Conflict, cuerpoAsignar: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarSedeFranja_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "El turno esta retirado del catalogo";
        var fakes = CrearTool(statusAsignar: HttpStatusCode.NotFound, cuerpoAsignar: cuerpo);

        var resultado = await Ejecutar(
            fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarSedeFranja_TraduceElRechazoDelDominio_Cuando400()
    {
        var fakes = CrearTool(statusAsignar: HttpStatusCode.BadRequest, cuerpoAsignar: Validacion400Json);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(AsignarSedeFranjaTool.Mensajes.RechazoDelDominio, Validacion400Json));
    }

    // CA-2: un turno_id que no es GUID responde el mensaje del .resx con el valor recibido, sin backend.
    [Fact]
    public async Task AsignarSedeFranja_RechazaSinLlamarANingunDominio_CuandoTurnoIdNoEsUnGuidConNombre()
    {
        const string Valor = "Nocturno";
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turnoId: Valor, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.TurnoIdInvalido, Valor));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarSedeFranja_RechazaSinLlamarANingunDominio_CuandoTurnoIdNoEsUnGuidEnBlanco()
    {
        const string Valor = "   ";
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turnoId: Valor, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.TurnoIdInvalido, Valor));
        AsegurarNingunaRequest(fakes);
    }

    // CA-5: la tool lee el detalle por identidad, nunca el catalogo completo.
    [Fact]
    public async Task AsignarSedeFranja_LeeElDetalleDelTurnoPorId_SinConsultarElCatalogo()
    {
        var fakes = CrearTool();

        await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Programacion.Requests.Should().Contain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurno);
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaCatalogo);
    }

    [Fact]
    public async Task AsignarSedeFranja_RechazaSinLlamarANingunDominio_CuandoFranjaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.CampoObligatorio, "franja"));
        AsegurarNingunaRequest(fakes);
    }

    // Boundary del sistema: un 5xx del catalogo o de la ficha de sede se traduce a texto, nunca a
    // excepcion (CA-ADR-0030) -- y corta antes de cualquier POST.
    [Fact]
    public async Task AsignarSedeFranja_RespondeElRechazoDelDominioSinPost_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Sedes.UltimaRequest.Should().BeNull("el catalogo se resuelve antes que la sede");
    }

    [Fact]
    public async Task AsignarSedeFranja_RespondeElRechazoDelDominioSinPost_CuandoLaFichaDeSedeFalla()
    {
        var fakes = CrearTool(sedeJson: "sedes caido", statusSede: HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(
            fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarSedeFranjaTool.Mensajes.RechazoDelDominio, "sedes caido"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
