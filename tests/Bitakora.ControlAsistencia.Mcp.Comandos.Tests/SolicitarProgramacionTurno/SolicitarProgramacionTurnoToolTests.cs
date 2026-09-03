using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.SolicitarProgramacionTurno;

public class SolicitarProgramacionTurnoToolTests
{
    private const string Turno = "Cocina Manana";
    private const string SedeCodigo = "SUBA";
    private const string VentanaDesde = "2026-09-01";
    private const string VentanaHasta = "2026-09-30";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");
    private static string SedeJson => Fixtures.Leer("SolicitarProgramacionTurno", "sede.json");
    private static string SedeInactivaJson => Fixtures.Leer("SolicitarProgramacionTurno", "sede-inactiva.json");
    private static string DirectorioJson => Fixtures.Leer("SolicitarProgramacionTurno", "directorio.json");
    private static string Validacion400Json => Fixtures.Leer("SolicitarProgramacionTurno", "validacion-400.json");

    private sealed record Fakes(
        SolicitarProgramacionTurnoTool Tool,
        HandlerPorRuta Programacion,
        HandlerEnlatado Sedes,
        HandlerEnlatado Colaboradores);

    // Composicion por defecto del camino feliz -- cada test sobreescribe unicamente el fixture que
    // le interesa via argumentos nombrados, mismo patron de consolidacion que RegistrarColaboradorToolTests.
    private static Fakes CrearTool(
        string? turnosJson = null,
        HttpStatusCode statusSolicitud = HttpStatusCode.Accepted,
        string cuerpoSolicitud = "",
        string? sedeJson = null,
        HttpStatusCode statusSede = HttpStatusCode.OK,
        string? directorioJson = null)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, turnosJson ?? TurnosJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaSolicitudes, statusSolicitud, cuerpoSolicitud);

        var (clienteSedes, handlerSedes) = ClienteFalso.Con(sedeJson ?? SedeJson, statusSede);
        var (clienteColaboradores, handlerColaboradores) = ClienteFalso.Con(
            directorioJson ?? DirectorioJson, HttpStatusCode.OK);

        var tool = new SolicitarProgramacionTurnoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));

        return new Fakes(tool, handlerProgramacion, handlerSedes, handlerColaboradores);
    }

    private static Task<string> Ejecutar(
        SolicitarProgramacionTurnoTool tool,
        string desde = VentanaDesde,
        string hasta = VentanaHasta,
        string turno = Turno,
        string sedeDeProgramacion = SedeCodigo,
        string identificaciones = "CC-1111,CC-2222,CC-3333",
        CancellationToken ct = default) =>
        tool.Run(
            context: null!,
            desde: desde,
            hasta: hasta,
            turno: turno,
            sedeDeProgramacion: sedeDeProgramacion,
            identificaciones: identificaciones,
            ct: ct);

    // CA-1: ningun rechazo local debe tocar ninguno de los 3 dominios.
    private static void AsegurarNingunaRequest(Fakes fakes)
    {
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Sedes.UltimaRequest.Should().BeNull();
        fakes.Colaboradores.UltimaRequest.Should().BeNull();
    }

    // CA-3/CA-4: tres pedidos -- CC-1111 (vigencia abierta, cubre toda la ventana), CC-2222
    // (vigencia terminada dentro de la ventana, recortado a 01..20) y CC-3333 (vigencia terminada
    // antes de la ventana, sin dias, omitido sin POST).
    [Fact]
    public async Task SolicitarProgramacionTurno_ProgramaSoloLosDiasQueCubreCadaVigencia_ConTresPedidos()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        var posts = fakes.Programacion.Requests
            .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
            .ToList();
        posts.Should().HaveCount(2, "CC-3333 no cubre ningun dia de la ventana y se omite sin POST");

        var cuerpoAna = JsonNode.Parse(posts.Single(p => p.Cuerpo!.Contains("AR01")).Cuerpo!)!;
        cuerpoAna["turnoId"]!.GetValue<string>().Should().Be("8f14e45f-ceea-4b3c-8f0a-000000000001");
        cuerpoAna["colaborador"]!["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        cuerpoAna["colaborador"]!["codigoColaborador"]!.GetValue<string>().Should().Be("AR01");
        cuerpoAna["colaborador"]!["nombreCompleto"]!.GetValue<string>().Should().Be("Ana Ruiz");
        cuerpoAna["fechas"]!.AsArray().Should().HaveCount(30);
        cuerpoAna["fechas"]![0]!.GetValue<string>().Should().Be("2026-09-01");
        cuerpoAna["fechas"]![29]!.GetValue<string>().Should().Be("2026-09-30");
        cuerpoAna["sede"]!["id"]!.GetValue<string>().Should().Be("SUBA");
        cuerpoAna["sede"]!["nombre"]!.GetValue<string>().Should().Be("Sede Suba");
        cuerpoAna["sede"]!["centroDeCostos"]!.GetValue<string>().Should().Be("CC-100");

        var cuerpoBeto = JsonNode.Parse(posts.Single(p => p.Cuerpo!.Contains("BD01")).Cuerpo!)!;
        cuerpoBeto["fechas"]!.AsArray().Should().HaveCount(20);
        cuerpoBeto["fechas"]![19]!.GetValue<string>().Should().Be("2026-09-20");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(SolicitarProgramacionTurnoTool.Mensajes.ResultadoProgramacionSolicitada);
        json["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        json["sede"]!["nombre"]!.GetValue<string>().Should().Be("Sede Suba");
        json["ventana"]!.GetValue<string>().Should().Be("2026-09-01 a 2026-09-30");
        json["omitidos"]!.GetValue<int>().Should().Be(1);
        json.AsObject().ContainsKey("fallidos").Should().BeFalse("ningun POST fallo en este escenario");
        json["nota"]!.GetValue<string>().Should().Be(SolicitarProgramacionTurnoTool.Mensajes.NotaVisibilidadEventual);

        var programados = json["programados"]!.AsArray();
        programados.Should().HaveCount(2);

        var programadoAna = programados.Single(p => p!["codigoColaborador"]!.GetValue<string>() == "AR01")!;
        programadoAna["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        programadoAna["nombre"]!.GetValue<string>().Should().Be("Ana Ruiz");
        programadoAna["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        programadoAna["hasta"]!.GetValue<string>().Should().Be("2026-09-30");
        programadoAna["dias"]!.GetValue<int>().Should().Be(30);

        var programadoBeto = programados.Single(p => p!["codigoColaborador"]!.GetValue<string>() == "BD01")!;
        programadoBeto["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        programadoBeto["hasta"]!.GetValue<string>().Should().Be("2026-09-20");
        programadoBeto["dias"]!.GetValue<int>().Should().Be(20);
    }

    // CA-2: coincidencia exacta bajo trim + colapsar espacios + case-insensitive.
    [Fact]
    public async Task SolicitarProgramacionTurno_ResuelveElTurnoPorNombre_ConTrimColapsoDeEspaciosYCaseInsensitive()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "  cocina   manana  ", identificaciones: "CC-1111",
            ct: TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
    }

    // CA-2: los acentos son significativos -- "Cocina Mañana" no es "Cocina Manana" del catalogo.
    [Fact]
    public async Task SolicitarProgramacionTurno_NoIgualaAcentos_CuandoElNombreDifiereSoloEnTildes()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "Cocina Mañana", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoTool.Mensajes.TurnoNoExiste, "Cocina Mañana", "Cocina Manana, Cocina Tarde"));
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RespondeTurnoNoExisteSinPost_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "Turno Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Cocina Manana, Cocina Tarde"));
        fakes.Programacion.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Get);
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Sedes.UltimaRequest.Should().BeNull("el turno se resuelve antes que la sede");
        fakes.Colaboradores.UltimaRequest.Should().BeNull("el turno se resuelve antes que el directorio");
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RespondeSedeNoExisteSinPost_CuandoLaSedeNoExiste()
    {
        var fakes = CrearTool(sedeJson: "", statusSede: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, sedeDeProgramacion: "NORTE", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.SedeNoExiste, "NORTE"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Colaboradores.UltimaRequest.Should().BeNull("la sede se resuelve antes que el directorio");
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RespondeSedeInactivaSinPost_CuandoLaSedeNoEstaActiva()
    {
        var fakes = CrearTool(sedeJson: SedeInactivaJson);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.SedeInactiva, "SUBA"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
        fakes.Colaboradores.UltimaRequest.Should().BeNull("la sede se resuelve antes que el directorio");
    }

    // CA-4: un POST que el dominio rechaza no detiene a los demas -- el resto del lote ya pudo
    // haberse programado y el agente necesita saberlo.
    [Fact]
    public async Task SolicitarProgramacionTurno_LlevaAFallidosAlColaboradorCuyoPostFalla_YProgramaAlOtro()
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        handlerProgramacion.Responde(HttpMethod.Post, RutaSolicitudes, (_, cuerpo) =>
            cuerpo!.Contains("AR01")
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(Validacion400Json, Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.Accepted));

        var (clienteSedes, _) = ClienteFalso.Con(SedeJson, HttpStatusCode.OK);
        var (clienteColaboradores, _) = ClienteFalso.Con(DirectorioJson, HttpStatusCode.OK);

        var tool = new SolicitarProgramacionTurnoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));

        var resultado = await Ejecutar(
            tool, identificaciones: "CC-1111,CC-2222", ct: TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should()
            .ContainSingle(p => p!["codigoColaborador"]!.GetValue<string>() == "BD01");
        var fallido = json["fallidos"]!.AsArray().Single(f => f!["identificacion"]!.GetValue<string>() == "CC-1111")!;
        fallido["motivo"]!.GetValue<string>().Should().Be(Validacion400Json);
    }

    // CA-3: un pedido con numero sin tipo nunca coincide exactamente con la identificacion completa
    // que devuelve el directorio -- queda omitido, sin POST, aunque el directorio SI conozca a esa
    // persona bajo "CC-1111".
    [Fact]
    public async Task SolicitarProgramacionTurno_OmiteAlColaborador_CuandoSePideConNumeroSinTipo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificaciones: "1111", ct: TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().BeEmpty();
        json["omitidos"]!.GetValue<int>().Should().Be(1);
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoDesdeEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "desde"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoHastaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, hasta: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "hasta"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "turno"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoSedeDeProgramacionEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, sedeDeProgramacion: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "sede_de_programacion"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoIdentificacionesEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificaciones: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "identificaciones"));
        AsegurarNingunaRequest(fakes);
    }

    // CA-1: identificaciones ausente en contenido util tras separar por coma y recortar -- distinto
    // del caso "en blanco" de arriba (una cadena con solo comas/espacios llega no-blank a la tool).
    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoIdentificacionesNoTraeNingunValorUtil()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificaciones: " , , ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(SolicitarProgramacionTurnoTool.Mensajes.CampoObligatorio, "identificaciones"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoDesdeTieneFormatoInvalido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-99-99", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(SolicitarProgramacionTurnoTool.Mensajes.FechaInvalida, "desde", "2026-99-99"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoHastaTieneFormatoInvalido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, hasta: "2026-99-99", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(
            string.Format(SolicitarProgramacionTurnoTool.Mensajes.FechaInvalida, "hasta", "2026-99-99"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, desde: "2026-09-10", hasta: "2026-09-01", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(SolicitarProgramacionTurnoTool.Mensajes.VentanaInvertida);
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoLaVentanaTiene32Dias()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, desde: "2026-09-01", hasta: "2026-10-02", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(SolicitarProgramacionTurnoTool.Mensajes.VentanaExcedeMaximo, 32));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_AceptaLaVentana_CuandoTieneExactamente31Dias()
    {
        var fakes = CrearTool(directorioJson: "[]");

        var resultado = await Ejecutar(
            fakes.Tool, desde: "2026-09-01", hasta: "2026-10-01", identificaciones: "CC-9999",
            ct: TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["ventana"]!.GetValue<string>().Should().Be("2026-09-01 a 2026-10-01");
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_RechazaSinLlamarANingunDominio_CuandoHayMasDe200Identificaciones()
    {
        var fakes = CrearTool();
        var identificaciones = string.Join(",", Enumerable.Range(1, 201).Select(i => $"CC-{i}"));

        var resultado = await Ejecutar(fakes.Tool, identificaciones: identificaciones, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoTool.Mensajes.DemasiadasIdentificaciones,
            SolicitarProgramacionTurnoTool.MaximoIdentificaciones));
        AsegurarNingunaRequest(fakes);
    }
}
