using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.QuitarSubFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.QuitarSubFranja;

public class QuitarSubFranjaToolTests
{
    private const string Turno = "Nocturno";
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000040";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaQuitarSubFranja = $"/api/programacion/turnos/{TurnoId}:quitar-subfranja";

    private static string TurnosConHijasJson => Fixtures.Leer("QuitarSubFranja", "turnos-con-hijas.json");
    private static string TurnosSinHijasJson => Fixtures.Leer("QuitarSubFranja", "turnos-sin-hijas.json");
    private static string Validacion400Json => Fixtures.Leer("QuitarSubFranja", "validacion-400.json");

    private sealed record Fakes(QuitarSubFranjaTool Tool, HandlerPorRuta Programacion);

    // Composicion por defecto del camino feliz (ficha vigente CON la sub-franja) -- cada test
    // sobreescribe unicamente el fixture que le interesa via argumentos nombrados.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusQuitar = HttpStatusCode.Accepted,
        string cuerpoQuitar = "")
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosConHijasJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaQuitarSubFranja, statusQuitar, cuerpoQuitar);

        var tool = new QuitarSubFranjaTool(new ProgramacionApi(clienteProgramacion));

        return new Fakes(tool, handlerProgramacion);
    }

    private static Task<string> Ejecutar(
        QuitarSubFranjaTool tool,
        string turno = Turno,
        string franja = "22:00",
        string tipo = "descanso",
        string inicio = "02:00",
        CancellationToken ct = default) =>
        tool.Run(context: null!, turno: turno, franja: franja, tipo: tipo, inicio: inicio, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Programacion.Requests.Should().BeEmpty();

    // Sin offsets en el body: los del eco salen de la ficha vigente, no de lo que se envio.
    [Fact]
    public async Task QuitarSubFranja_EnviaElBodyConLasHoras_YComponeElEcoConLosOffsetsDeLaFichaVigente()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["franja"]!.GetValue<string>().Should().Be("22:00");
        body["tipo"]!.GetValue<string>().Should().Be("descanso");
        body["inicio"]!.GetValue<string>().Should().Be("02:00");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(QuitarSubFranjaTool.Mensajes.ResultadoSubFranjaQuitada);
        json["turno"]!.GetValue<string>().Should().Be(Turno);
        json["franja"]!.GetValue<string>().Should().Be("22:00");
        json["subFranjaQuitada"]!.GetValue<string>().Should().Be("descanso 02:00+1-02:30+1");
        json["nota"]!.GetValue<string>().Should().Be(QuitarSubFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task QuitarSubFranja_ComponeElEcoDesdeLaListaDeExtras_CuandoElTipoEsExtra()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, tipo: "extra", inicio: "23:00", ct: TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["subFranjaQuitada"]!.GetValue<string>().Should().Be("extra 23:00-23:30");
    }

    // El POST se envia igual aunque la ficha no muestre la sub-franja: el 409 lo decide el dominio.
    [Fact]
    public async Task QuitarSubFranja_ComponeElEcoSoloConTipoYHora_CuandoLaFichaVigenteAunNoMuestraLaSubFranja()
    {
        var fakes = CrearTool(turnosJson: TurnosSinHijasJson);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Programacion.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Post);
        JsonNode.Parse(resultado)!["subFranjaQuitada"]!.GetValue<string>().Should().Be("descanso 02:00");
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoTipoEsDesconocido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, tipo: "pausa", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.TipoDesconocido, "pausa"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoFranjaNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "10pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.HoraInvalida, "franja", "10pm"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoInicioNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "2am", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.HoraInvalida, "inicio", "2am"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RespondeTurnoNoExisteSinPost_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "Turno Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            QuitarSubFranjaTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Nocturno"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task QuitarSubFranja_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "No existe un descanso que empiece a las 02:00";
        var fakes = CrearTool(statusQuitar: HttpStatusCode.Conflict, cuerpoQuitar: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarSubFranja_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusQuitar: HttpStatusCode.BadRequest, cuerpoQuitar: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.CampoObligatorio, "turno"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoFranjaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.CampoObligatorio, "franja"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoTipoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, tipo: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.CampoObligatorio, "tipo"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarSubFranja_RechazaSinLlamarANingunDominio_CuandoInicioEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.CampoObligatorio, "inicio"));
        AsegurarNingunaRequest(fakes);
    }

    // Boundary del sistema: un 5xx del catalogo se traduce a texto, nunca a excepcion
    // (CA-ADR-0030) -- y corta antes de cualquier POST.
    [Fact]
    public async Task QuitarSubFranja_RespondeElRechazoDelDominioSinPost_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarSubFranjaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
