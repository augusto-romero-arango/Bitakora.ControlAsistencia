using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.AgregarFranja;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.AgregarFranja;

public class AgregarFranjaToolTests
{
    private const string Turno = "Nocturno";
    private const string TurnoId = "8f14e45f-ceea-4b3c-8f0a-000000000010";
    private const string SedeCodigo = "SUBA";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaAgregarFranja = $"/api/programacion/turnos/{TurnoId}:agregar-franja";

    private static string TurnosJson => Fixtures.Leer("AgregarFranja", "turnos.json");
    private static string SedeJson => Fixtures.Leer("AgregarFranja", "sede.json");
    private static string SedeInactivaJson => Fixtures.Leer("AgregarFranja", "sede-inactiva.json");
    private static string Validacion400Json => Fixtures.Leer("AgregarFranja", "validacion-400.json");

    private sealed record Fakes(AgregarFranjaTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Sedes);

    // Composicion por defecto del camino feliz -- cada test sobreescribe unicamente el fixture que
    // le interesa via argumentos nombrados, mismo patron de consolidacion que las demas tools.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusAgregar = HttpStatusCode.Accepted,
        string cuerpoAgregar = "",
        string? sedeJson = null,
        HttpStatusCode statusSede = HttpStatusCode.OK)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaAgregarFranja, statusAgregar, cuerpoAgregar);

        var (clienteSedes, handlerSedes) = ClienteFalso.Con(sedeJson ?? SedeJson, statusSede);

        var tool = new AgregarFranjaTool(new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes));

        return new Fakes(tool, handlerProgramacion, handlerSedes);
    }

    private static Task<string> Ejecutar(
        AgregarFranjaTool tool,
        string turno = Turno,
        string inicio = "22:00",
        string fin = "06:00",
        string? codigoSede = null,
        CancellationToken ct = default) =>
        tool.Run(context: null!, turno: turno, inicio: inicio, fin: fin, codigoSede: codigoSede, ct: ct);

    // CA-1/CA-3: ningun rechazo local debe tocar ninguno de los 2 dominios.
    private static void AsegurarNingunaRequest(Fakes fakes)
    {
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Sedes.UltimaRequest.Should().BeNull();
    }

    // CA-1: sin codigo_sede, el body no lleva diaOffsetFin ni sede; el eco trae solo el rango.
    [Fact]
    public async Task AgregarFranja_EnviaElBodySinDiaOffsetFinNiSede_CuandoNoHayCodigoSede()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["inicio"]!.GetValue<string>().Should().Be("22:00");
        body["fin"]!.GetValue<string>().Should().Be("06:00");
        body.AsObject().ContainsKey("diaOffsetFin").Should().BeFalse();
        body.AsObject().ContainsKey("sede").Should().BeFalse();
        fakes.Sedes.UltimaRequest.Should().BeNull("sin codigo_sede no debe consultarse Sedes");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AgregarFranjaTool.Mensajes.ResultadoFranjaAgregada);
        json["turno"]!.GetValue<string>().Should().Be(Turno);
        json["franja"]!.GetValue<string>().Should().Be("22:00-06:00");
        json["nota"]!.GetValue<string>().Should().Be(AgregarFranjaTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-2: con codigo_sede, el body agrega sede {id, nombre, centroDeCostos} y el eco la incluye.
    [Fact]
    public async Task AgregarFranja_EnviaLaSedePrearmada_CuandoLlegaCodigoSede()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        fakes.Sedes.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/sedes/fichas/{SedeCodigo}");
        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        var body = JsonNode.Parse(post.Cuerpo!)!;
        body["sede"]!["id"]!.GetValue<string>().Should().Be(SedeCodigo);
        body["sede"]!["nombre"]!.GetValue<string>().Should().Be("Suba");
        body["sede"]!["centroDeCostos"]!.GetValue<string>().Should().Be("CC-100");

        JsonNode.Parse(resultado)!["franja"]!.GetValue<string>().Should().Be("22:00-06:00, sede: Suba");
    }

    [Fact]
    public async Task AgregarFranja_RespondeSedeNoExisteSinPost_CuandoLaSedeNoExiste()
    {
        var fakes = CrearTool(sedeJson: "", statusSede: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, codigoSede: "NORTE", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.SedeNoExiste, "NORTE"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task AgregarFranja_RespondeSedeInactivaSinPost_CuandoLaSedeNoEstaActiva()
    {
        var fakes = CrearTool(sedeJson: SedeInactivaJson);

        var resultado = await Ejecutar(fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.SedeInactiva, SedeCodigo));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    // CA-3: inicio == fin es la unica lectura valida de una franja de 24h -- se traduce a
    // diaOffsetFin: 1, nunca calculado por la tool a partir de otra cosa que no sea esta igualdad.
    [Fact]
    public async Task AgregarFranja_EnviaDiaOffsetFinUno_CuandoInicioYFinSonIguales()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, inicio: "08:00", fin: "08:00", ct: TestContext.Current.CancellationToken);

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        JsonNode.Parse(post.Cuerpo!)!["diaOffsetFin"]!.GetValue<int>().Should().Be(1);

        JsonNode.Parse(resultado)!["franja"]!.GetValue<string>().Should().Be("08:00-08:00+1");
    }

    [Fact]
    public async Task AgregarFranja_RechazaSinLlamarANingunDominio_CuandoInicioNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "8pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.HoraInvalida, "inicio", "8pm"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarFranja_RechazaSinLlamarANingunDominio_CuandoFinNoEsUnaHoraValida()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, fin: "8pm", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.HoraInvalida, "fin", "8pm"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarFranja_RespondeTurnoNoExisteSinPost_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "Turno Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            AgregarFranjaTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Nocturno, Cocina Manana"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Sedes.UltimaRequest.Should().BeNull("el turno se resuelve antes que la sede");
    }

    [Fact]
    public async Task AgregarFranja_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "La franja se solapa con otra existente";
        var fakes = CrearTool(statusAgregar: HttpStatusCode.Conflict, cuerpoAgregar: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AgregarFranja_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusAgregar: HttpStatusCode.BadRequest, cuerpoAgregar: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task AgregarFranja_RechazaSinLlamarANingunDominio_CuandoTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.CampoObligatorio, "turno"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarFranja_RechazaSinLlamarANingunDominio_CuandoInicioEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, inicio: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.CampoObligatorio, "inicio"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AgregarFranja_RechazaSinLlamarANingunDominio_CuandoFinEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, fin: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.CampoObligatorio, "fin"));
        AsegurarNingunaRequest(fakes);
    }

    // Boundary del sistema: un 5xx del catalogo o de la ficha de sede se traduce a texto, nunca a
    // excepcion (CA-ADR-0030) -- y corta antes de cualquier POST.
    [Fact]
    public async Task AgregarFranja_RespondeElRechazoDelDominioSinPost_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Sedes.UltimaRequest.Should().BeNull("el catalogo se resuelve antes que la sede");
    }

    [Fact]
    public async Task AgregarFranja_RespondeElRechazoDelDominioSinPost_CuandoLaFichaDeSedeFalla()
    {
        var fakes = CrearTool(sedeJson: "sedes caido", statusSede: HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(
            fakes.Tool, codigoSede: SedeCodigo, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AgregarFranjaTool.Mensajes.RechazoDelDominio, "sedes caido"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
