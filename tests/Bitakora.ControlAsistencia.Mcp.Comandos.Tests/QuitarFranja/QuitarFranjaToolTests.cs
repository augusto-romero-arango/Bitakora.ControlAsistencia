using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.QuitarFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.QuitarFranja;

public class QuitarFranjaToolTests
{
    private const string Turno = "Nocturno";
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000020";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaQuitarFranja = $"/api/programacion/turnos/{TurnoId}:quitar-franja";

    private static string TurnosConFranjaJson => Fixtures.Leer("QuitarFranja", "turnos-con-franja.json");
    private static string TurnosSinLaFranjaJson => Fixtures.Leer("QuitarFranja", "turnos-sin-la-franja.json");
    private static string Validacion400Json => Fixtures.Leer("QuitarFranja", "validacion-400.json");

    private sealed record Fakes(QuitarFranjaTool Tool, HandlerPorRuta Programacion);

    // Composicion por defecto del camino feliz (ficha vigente CON la franja) -- cada test
    // sobreescribe unicamente el fixture que le interesa via argumentos nombrados.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusQuitar = HttpStatusCode.Accepted,
        string cuerpoQuitar = "")
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosConFranjaJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaQuitarFranja, statusQuitar, cuerpoQuitar);

        var tool = new QuitarFranjaTool(new ProgramacionApi(clienteProgramacion));

        return new Fakes(tool, handlerProgramacion);
    }

    private static Task<string> Ejecutar(
        QuitarFranjaTool tool, string turno = Turno, string franja = "15:00", CancellationToken ct = default) =>
        tool.Run(context: null!, turno: turno, franja: franja, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Programacion.Requests.Should().BeEmpty();

    // CA-4: el body siempre viaja con la hora tal cual, y el eco compone la franja completa desde
    // la ficha vigente al momento de la llamada (descansos, extras y sede incluidos).
    [Fact]
    public async Task QuitarFranja_EnviaElBodyConLaHora_YComponeElEcoDesdeLaFichaVigente()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        JsonNode.Parse(post.Cuerpo!)!["franja"]!.GetValue<string>().Should().Be("15:00");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(QuitarFranjaTool.Mensajes.ResultadoFranjaQuitada);
        json["turno"]!.GetValue<string>().Should().Be(Turno);
        json["franjaQuitada"]!.GetValue<string>().Should().Be("15:00-19:00, descanso 12:00-13:00, sede: Suba");
        json["nota"]!.GetValue<string>().Should().Be(QuitarFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-4: si la ficha vigente aun no muestra la franja (visibilidad eventual), el eco trae solo
    // la hora -- pero el POST se envia igual: el dominio decide con 409 si en verdad no existe.
    [Fact]
    public async Task QuitarFranja_ComponeElEcoSoloConLaHora_CuandoLaFichaVigenteAunNoMuestraLaFranja()
    {
        var fakes = CrearTool(turnosJson: TurnosSinLaFranjaJson);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Programacion.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Post);
        JsonNode.Parse(resultado)!["franjaQuitada"]!.GetValue<string>().Should().Be("15:00");
    }

    [Fact]
    public async Task QuitarFranja_RespondeTurnoNoExisteSinPost_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "Turno Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            QuitarFranjaTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Nocturno"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task QuitarFranja_RechazaSinLlamarANingunDominio_CuandoTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.CampoObligatorio, "turno"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarFranja_RechazaSinLlamarANingunDominio_CuandoFranjaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.CampoObligatorio, "franja"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarFranja_RechazaSinLlamarANingunDominio_CuandoFranjaNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, franja: "3pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.HoraInvalida, "franja", "3pm"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarFranja_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "No existe una franja que empiece a las 15:00";
        var fakes = CrearTool(statusQuitar: HttpStatusCode.Conflict, cuerpoQuitar: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarFranja_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusQuitar: HttpStatusCode.BadRequest, cuerpoQuitar: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.RechazoDelDominio, fixture));
    }

    // Boundary del sistema: un 5xx del catalogo se traduce a texto, nunca a excepcion
    // (CA-ADR-0030) -- y corta antes de cualquier POST.
    [Fact]
    public async Task QuitarFranja_RespondeElRechazoDelDominioSinPost_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarFranjaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
