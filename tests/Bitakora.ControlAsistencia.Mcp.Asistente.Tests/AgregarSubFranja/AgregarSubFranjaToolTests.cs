using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AgregarSubFranja;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AgregarSubFranja;

public class AgregarSubFranjaToolTests
{
    private const string Turno = "Nocturno";
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000030";
    private const string RutaCatalogo = "/api/programacion/turnos";
    private const string RutaTurno = $"/api/programacion/turnos/{TurnoId}";
    private const string RutaAgregarSubFranja = $"/api/programacion/turnos/{TurnoId}:agregar-subfranja";

    private static string TurnosJson => Fixtures.Leer("AgregarSubFranja", "turno.json");
    private static string Validacion400Json => Fixtures.Leer("AgregarSubFranja", "validacion-400.json");

    private sealed record Fakes(AgregarSubFranjaTool Tool, HandlerPorRuta Programacion);

    // Composicion por defecto del camino feliz -- cada test sobreescribe unicamente el fixture que
    // le interesa via argumentos nombrados, mismo patron que las demas tools de diseno de turno.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusAgregar = HttpStatusCode.NoContent,
        string cuerpoAgregar = "")
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurno, statusTurnos, turnosJson ?? TurnosJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaAgregarSubFranja, statusAgregar, cuerpoAgregar);

        var programacionApi = new ProgramacionApi(clienteProgramacion);
        var tool = new AgregarSubFranjaTool(programacionApi, new ResolutorTurnoPorId(programacionApi));

        return new Fakes(tool, handlerProgramacion);
    }

    private static Task<string> Ejecutar(
        AgregarSubFranjaTool tool,
        string turnoId = TurnoId,
        string franja = "22:00",
        string tipo = "descanso",
        string inicio = "02:00",
        string fin = "02:30",
        CancellationToken ct = default) =>
        tool.Run(context: null!, turnoId: turnoId, franja: franja, tipo: tipo, inicio: inicio, fin: fin, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Programacion.Requests.Should().BeEmpty();

    // Sin offsets en el body: los infiere el dominio a partir de la hora, la tool nunca los calcula.
    [Fact]
    public async Task AgregarSubFranja_EnviaElBodySinOffsets_YComponeElEcoConElRangoEnviado()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["franja"]!.GetValue<string>().Should().Be("22:00");
        body["tipo"]!.GetValue<string>().Should().Be("descanso");
        body["inicio"]!.GetValue<string>().Should().Be("02:00");
        body["fin"]!.GetValue<string>().Should().Be("02:30");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AgregarSubFranjaTool.Mensajes.ResultadoSubFranjaAgregada);
        json["turno"]!.GetValue<string>().Should().Be(Turno);
        json["franja"]!.GetValue<string>().Should().Be("22:00");
        json["subFranja"]!.GetValue<string>().Should().Be("descanso 02:00-02:30");
        json["nota"]!.GetValue<string>().Should().Be(AgregarSubFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task AgregarSubFranja_NormalizaElTipoAMinusculas_CuandoLlegaCapitalizado()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, tipo: "Extra", ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        JsonNode.Parse(post.Cuerpo!)!["tipo"]!.GetValue<string>().Should().Be("extra");

        JsonNode.Parse(resultado)!["subFranja"]!.GetValue<string>().Should().Be("extra 02:00-02:30");
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoTipoEsDesconocido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, tipo: "pausa", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.TipoDesconocido, "pausa"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoFranjaNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "10pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.HoraInvalida, "franja", "10pm"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoInicioNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "2am", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.HoraInvalida, "inicio", "2am"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoFinNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, fin: "230am", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.HoraInvalida, "fin", "230am"));
        AsegurarNingunaRequest(fakes);
    }

    // CA-3: el detalle por identidad responde 404 -> frase con el id que remite a buscar_turnos,
    // sin lista de nombres y sin invocar el comando.
    [Fact]
    public async Task AgregarSubFranja_RespondeTurnoNoExisteSinPost_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.TurnoNoExiste, TurnoId));
        resultado.Should().Contain(TurnoId).And.Contain("buscar_turnos").And.NotContain("disponibles");
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task AgregarSubFranja_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "La sub-franja se solapa con otra existente";
        var fakes = CrearTool(statusAgregar: HttpStatusCode.Conflict, cuerpoAgregar: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AgregarSubFranja_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusAgregar: HttpStatusCode.BadRequest, cuerpoAgregar: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.RechazoDelDominio, fixture));
    }

    // CA-2: un turno_id que no es GUID responde el mensaje del .resx con el valor recibido, sin backend.
    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoTurnoIdNoEsUnGuidConNombre()
    {
        const string Valor = "Nocturno";
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turnoId: Valor, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.TurnoIdInvalido, Valor));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoTurnoIdNoEsUnGuidEnBlanco()
    {
        const string Valor = "   ";
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turnoId: Valor, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.TurnoIdInvalido, Valor));
        AsegurarNingunaRequest(fakes);
    }

    // CA-5: la tool lee el detalle por identidad, nunca el catalogo completo.
    [Fact]
    public async Task AgregarSubFranja_LeeElDetalleDelTurnoPorId_SinConsultarElCatalogo()
    {
        var fakes = CrearTool();

        await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Programacion.Requests.Should().Contain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurno);
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaCatalogo);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoFranjaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.CampoObligatorio, "franja"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoTipoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, tipo: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.CampoObligatorio, "tipo"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoInicioEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.CampoObligatorio, "inicio"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarSubFranja_RechazaSinLlamarANingunDominio_CuandoFinEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, fin: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.CampoObligatorio, "fin"));
        AsegurarNingunaRequest(fakes);
    }

    // Boundary del sistema: un 5xx del catalogo se traduce a texto, nunca a excepcion
    // (CA-ADR-0030) -- y corta antes de cualquier POST.
    [Fact]
    public async Task AgregarSubFranja_RespondeElRechazoDelDominioSinPost_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarSubFranjaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
