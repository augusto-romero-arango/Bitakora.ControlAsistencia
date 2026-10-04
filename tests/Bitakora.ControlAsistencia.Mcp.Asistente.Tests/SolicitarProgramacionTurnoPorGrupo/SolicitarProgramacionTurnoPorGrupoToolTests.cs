using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurnoPorGrupo;

public class SolicitarProgramacionTurnoPorGrupoToolTests
{
    private const string Turno = "Cocina Manana";
    private const string SedeProgramacion = "SUBA";
    private const string VentanaDesde = "2026-09-01";
    private const string VentanaHasta = "2026-09-30";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";
    private const string RutaFichas = "/api/colaboradores/fichas";
    private const int TamanoPagina = 200;

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");
    private static string SedeJson => Fixtures.Leer("SolicitarProgramacionTurno", "sede.json");
    private static string SedeInactivaJson => Fixtures.Leer("SolicitarProgramacionTurno", "sede-inactiva.json");

    private static string SedeConCodigo(string codigo, string nombre) =>
        SedeJson.Replace("\"codigo\": \"SUBA\"", $"\"codigo\": \"{codigo}\"")
            .Replace("\"nombre\": \"Sede Suba\"", $"\"nombre\": \"{nombre}\"");

    private static object Ficha(
        int numero, string vigenteDesde = "2025-01-01", string? vigenteHasta = null) => new
        {
            id = $"CC-{numero:D4}",
            nombreCompleto = $"Colab {numero:D4}",
            codigoColaborador = $"C{numero:D4}",
            vigenteDesde,
            vigenteHasta,
            etiquetas = Array.Empty<object>(),
            codigoSede = "SUBA"
        };

    private static List<object> Fichas(int desde, int cantidad) =>
        [.. Enumerable.Range(desde, cantidad).Select(n => Ficha(n))];

    private sealed class Entorno
    {
        public required SolicitarProgramacionTurnoPorGrupoTool Tool { get; init; }
        public required HandlerPorRuta Programacion { get; init; }
        public required HandlerFuncional Sedes { get; init; }
        public required HandlerFuncional Colaboradores { get; init; }
        public required List<JsonNode> ConsultasDeFichas { get; init; }

        public List<(HttpMethod Metodo, string Ruta, string? Cuerpo)> Posts =>
            [.. Programacion.Requests.Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)];
    }

    // paginas: una lista de fichas por pagina; la tool avanza de pagina cuando el body trae cursor.
    private static Entorno CrearEntorno(
        IReadOnlyList<IReadOnlyList<object>>? paginas = null,
        Func<string?, HttpResponseMessage>? respuestaPost = null,
        string? turnosJson = null,
        Dictionary<string, (HttpStatusCode Status, string Cuerpo)>? sedesPorCodigo = null)
    {
        paginas ??= [[]];
        var consultas = new List<JsonNode>();
        var cursoresVistos = 0;

        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, turnosJson ?? TurnosJson);
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? Respuesta(HttpStatusCode.Created, ""));

        var sedesConfiguradas = sedesPorCodigo ?? new()
        {
            [SedeProgramacion] = (HttpStatusCode.OK, SedeJson)
        };
        var (clienteSedes, sedes) = ClienteFalso.ConFuncion(request =>
        {
            var codigo = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/').Last());
            return sedesConfiguradas.TryGetValue(codigo, out var r)
                ? Respuesta(r.Status, r.Cuerpo)
                : Respuesta(HttpStatusCode.NotFound, "");
        });

        var (clienteColaboradores, colaboradores) = ClienteFalso.ConFuncion(request =>
        {
            var cuerpo = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var nodo = JsonNode.Parse(cuerpo)!;
            lock (consultas)
                consultas.Add(nodo);

            var pagina = nodo["cursor"] is null ? 0 : ++cursoresVistos;
            var fichas = pagina < paginas.Count ? paginas[pagina] : [];
            return Respuesta(HttpStatusCode.OK, JsonSerializer.Serialize(
                fichas, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        });

        return new Entorno
        {
            Tool = new SolicitarProgramacionTurnoPorGrupoTool(
                new ProgramacionApi(clienteProgramacion),
                new SedesApi(clienteSedes),
                new ColaboradoresApi(clienteColaboradores)),
            Programacion = programacion,
            Sedes = sedes,
            Colaboradores = colaboradores,
            ConsultasDeFichas = consultas
        };
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static Task<string> Ejecutar(
        Entorno entorno,
        string desde = VentanaDesde,
        string hasta = VentanaHasta,
        string turno = Turno,
        string sedeDeProgramacion = SedeProgramacion,
        string? sede = null,
        string? etiquetas = "area:cocina") =>
        entorno.Tool.Run(
            context: null!,
            desde: desde,
            hasta: hasta,
            turno: turno,
            sedeDeProgramacion: sedeDeProgramacion,
            sede: sede,
            etiquetas: etiquetas,
            ct: TestContext.Current.CancellationToken);

    private static void AsegurarSinEscrituras(Entorno entorno)
    {
        entorno.Posts.Should().BeEmpty();
        entorno.ConsultasDeFichas.Should().BeEmpty();
    }

    // CA-2
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoNoLlegaSedeNiEtiquetas()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, sede: null, etiquetas: null);

        resultado.Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SelectorObligatorio);
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoSedeYEtiquetasVienenEnBlanco()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, sede: "  ", etiquetas: " ");

        resultado.Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SelectorObligatorio);
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaNombrandoElPar_CuandoUnaEtiquetaNoTieneDosPuntos()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, etiquetas: "area:cocina, area");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.EtiquetaMalFormada, "area"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaNombrandoElPar_CuandoElValorDeLaEtiquetaEstaVacio()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, etiquetas: "area:");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.EtiquetaMalFormada, "area:"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaNombrandoElPar_CuandoLaCategoriaDeLaEtiquetaEstaVacia()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, etiquetas: ":cocina");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.EtiquetaMalFormada, ":cocina"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaSedeDelSelectorNoExiste()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, sede: "FANTASMA", etiquetas: null);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SedeDelSelectorNoExiste, "FANTASMA"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaSedeDelSelectorEstaInactiva()
    {
        var entorno = CrearEntorno(sedesPorCodigo: new()
        {
            [SedeProgramacion] = (HttpStatusCode.OK, SedeJson),
            ["CERRADA"] = (HttpStatusCode.OK, SedeInactivaJson)
        });

        var resultado = await Ejecutar(entorno, sede: "CERRADA", etiquetas: null);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SedeDelSelectorInactiva, "CERRADA"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaVentanaExcedeLosTreintaYCincoDias()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, desde: "2026-10-07", hasta: "2026-11-11");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.VentanaExcedeMaximo, 36));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaVentanaEstaInvertida()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, desde: "2026-09-10", hasta: "2026-09-01");

        resultado.Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.VentanaInvertida);
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaFechaNoTieneFormatoIso()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, desde: "01/09/2026");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.FechaInvalida, "desde", "01/09/2026"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoElTurnoNoExiste()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, turno: "Turno Fantasma");

        resultado.Should().StartWith("No existe un turno con el nombre 'Turno Fantasma'");
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaSedeDeProgramacionNoExiste()
    {
        var entorno = CrearEntorno();

        var resultado = await Ejecutar(entorno, sedeDeProgramacion: "FANTASMA");

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SedeNoExiste, "FANTASMA"));
        AsegurarSinEscrituras(entorno);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaSinEscribir_CuandoLaSedeDeProgramacionEstaInactiva()
    {
        var entorno = CrearEntorno(sedesPorCodigo: new()
        {
            [SedeProgramacion] = (HttpStatusCode.OK, SedeInactivaJson)
        });

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.SedeInactiva, SedeProgramacion));
        AsegurarSinEscrituras(entorno);
    }

    // CA-3
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_ProgramaATodosLosDeLasDosPaginas_CuandoElGrupoSuperaLaPagina()
    {
        var entorno = CrearEntorno(paginas: [Fichas(1, TamanoPagina), Fichas(TamanoPagina + 1, 3)]);

        var resultado = await Ejecutar(entorno);

        entorno.ConsultasDeFichas.Should().HaveCount(2);
        entorno.ConsultasDeFichas[0]["cursor"].Should().BeNull("la primera pagina no lleva cursor");
        var cursor = entorno.ConsultasDeFichas[1]["cursor"]!;
        cursor["nombreCompleto"]!.GetValue<string>().Should().Be("Colab 0200");
        cursor["id"]!.GetValue<string>().Should().Be("CC-0200");
        entorno.ConsultasDeFichas.Should().AllSatisfy(c => c["take"]!.GetValue<int>().Should().Be(TamanoPagina));

        entorno.Posts.Should().HaveCount(203);
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(203);
        json["grupoResuelto"]!.GetValue<int>().Should().Be(203);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoPideOtraPagina_CuandoLaPaginaTrajoMenosDeDoscientas()
    {
        var entorno = CrearEntorno(paginas: [Fichas(1, 5)]);

        await Ejecutar(entorno);

        entorno.ConsultasDeFichas.Should().HaveCount(1);
        entorno.Posts.Should().HaveCount(5);
    }

    // CA-4
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EnviaSedeCanonicaYEtiquetasJuntas_CuandoVienenAmbas()
    {
        var entorno = CrearEntorno(
            paginas: [Fichas(1, 1)],
            sedesPorCodigo: new()
            {
                [SedeProgramacion] = (HttpStatusCode.OK, SedeJson),
                ["suba norte"] = (HttpStatusCode.OK, SedeConCodigo("SUBA-N", "Sede Suba Norte"))
            });

        await Ejecutar(entorno, sede: "suba norte", etiquetas: "area:cocina, Turno:Noche");

        var consulta = entorno.ConsultasDeFichas.Single();
        consulta["fechaReferencia"]!.GetValue<string>().Should().Be(VentanaDesde);
        consulta["codigoSede"]!.GetValue<string>().Should().Be("SUBA-N");
        var etiquetas = consulta["etiquetas"]!.AsArray();
        etiquetas.Should().HaveCount(2);
        etiquetas[0]!["categoria"]!.GetValue<string>().Should().Be("area");
        etiquetas[0]!["valor"]!.GetValue<string>().Should().Be("cocina");
        etiquetas[1]!["categoria"]!.GetValue<string>().Should().Be("Turno");
        etiquetas[1]!["valor"]!.GetValue<string>().Should().Be("Noche");
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoEnviaCodigoDeSede_CuandoSoloLlegaronEtiquetas()
    {
        var entorno = CrearEntorno(paginas: [Fichas(1, 1)]);

        await Ejecutar(entorno, sede: null, etiquetas: "area:cocina");

        var consulta = entorno.ConsultasDeFichas.Single();
        consulta["codigoSede"].Should().BeNull();
        consulta["etiquetas"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoEnviaEtiquetas_CuandoSoloLlegoLaSede()
    {
        var entorno = CrearEntorno(paginas: [Fichas(1, 1)]);

        await Ejecutar(entorno, sede: SedeProgramacion, etiquetas: null);

        var consulta = entorno.ConsultasDeFichas.Single();
        consulta["codigoSede"]!.GetValue<string>().Should().Be("SUBA");
        (consulta["etiquetas"]?.AsArray() ?? []).Should().BeEmpty();
    }

    // CA-5
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RecortaPorVinculacionYOmiteSinDias_CuandoLaVigenciaNoCubreLaVentana()
    {
        var entorno = CrearEntorno(paginas:
        [[
            Ficha(1),
            Ficha(2, vigenteDesde: "2026-09-16"),
            Ficha(3, vigenteDesde: "2024-01-01", vigenteHasta: "2026-08-30")
        ]]);

        var resultado = await Ejecutar(entorno);

        entorno.Posts.Should().HaveCount(2, "el tercero no cubre ningun dia y no genera POST");
        var cuerpoMitad = JsonNode.Parse(entorno.Posts.Single(p => p.Cuerpo!.Contains("C0002")).Cuerpo!)!;
        cuerpoMitad["fechas"]!.AsArray().Should().HaveCount(15);
        cuerpoMitad["fechas"]![0]!.GetValue<string>().Should().Be("2026-09-16");
        cuerpoMitad["colaborador"]!["identificacion"]!.GetValue<string>().Should().Be("CC-0002");
        cuerpoMitad["sede"]!["id"]!.GetValue<string>().Should().Be("SUBA");

        var json = JsonNode.Parse(resultado)!;
        json["omitidos"]!.GetValue<int>().Should().Be(1);
        json["grupoResuelto"]!.GetValue<int>().Should().Be(3);
        json["programados"]!.AsArray().Should().HaveCount(2);
    }

    // CA-6
    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_ReportaTurnoSedeVentanaYSelector_CuandoProgramaElGrupo()
    {
        var entorno = CrearEntorno(paginas: [Fichas(1, 2)]);

        var resultado = await Ejecutar(entorno, sede: SedeProgramacion, etiquetas: "area:cocina");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.ResultadoProgramacionSolicitada);
        json["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        json["sede"]!["nombre"]!.GetValue<string>().Should().Be("Sede Suba");
        json["ventana"]!.GetValue<string>().Should().Be("2026-09-01 a 2026-09-30");
        json["selector"]!.GetValue<string>().Should().Contain("SUBA").And.Contain("area:cocina");
        json["grupoResuelto"]!.GetValue<int>().Should().Be(2);
        json["omitidos"]!.GetValue<int>().Should().Be(0);
        json.AsObject().ContainsKey("fallidos").Should().BeFalse();
        json["nota"]!.GetValue<string>()
            .Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.NotaVisibilidadEventual);

        var primero = json["programados"]!.AsArray()[0]!;
        primero["identificacion"]!.GetValue<string>().Should().Be("CC-0001");
        primero["nombre"]!.GetValue<string>().Should().Be("Colab 0001");
        primero["codigoColaborador"]!.GetValue<string>().Should().Be("C0001");
        primero["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        primero["hasta"]!.GetValue<string>().Should().Be("2026-09-30");
        primero["dias"]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_InformaLosDiasRespetados_CuandoElDominioRespetaUnaAusencia()
    {
        var entorno = CrearEntorno(
            paginas: [Fichas(1, 2)],
            respuestaPost: cuerpo => cuerpo!.Contains("C0001")
                ? Respuesta(HttpStatusCode.Created,
                    """{"fechasRespetadas":[{"fecha":"2026-09-10","motivo":"Vacaciones"}]}""")
                : Respuesta(HttpStatusCode.Created, ""));

        var resultado = await Ejecutar(entorno);

        var programados = JsonNode.Parse(resultado)!["programados"]!.AsArray();
        var conAusencia = programados.Single(p => p!["codigoColaborador"]!.GetValue<string>() == "C0001")!;
        conAusencia["dias"]!.GetValue<int>().Should().Be(29);
        var respetado = conAusencia["respetados"]!.AsArray().Single()!;
        respetado["motivo"]!.GetValue<string>().Should().Be("Vacaciones");
        respetado["tramos"]!.GetValue<string>().Should().Be("10");
        programados.Single(p => p!["codigoColaborador"]!.GetValue<string>() == "C0002")!
            ["dias"]!.GetValue<int>().Should().Be(30);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_ReportaFallidosConMotivoYSigueConElResto_CuandoUnPostSeRechaza()
    {
        var entorno = CrearEntorno(
            paginas: [Fichas(1, 3)],
            respuestaPost: cuerpo => cuerpo!.Contains("C0002")
                ? Respuesta(HttpStatusCode.Conflict, "Turno incompleto")
                : Respuesta(HttpStatusCode.Created, ""));

        var resultado = await Ejecutar(entorno);

        entorno.Posts.Should().HaveCount(3);
        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().HaveCount(2);
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["identificacion"]!.GetValue<string>().Should().Be("CC-0002");
        fallido["motivo"]!.GetValue<string>().Should().Contain("Turno incompleto");
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_ReportaCeroProgramadosSinRechazo_CuandoElGrupoEstaVacio()
    {
        var entorno = CrearEntorno(paginas: [[]]);

        var resultado = await Ejecutar(entorno, sede: null, etiquetas: "area:cocina");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>()
            .Should().Be(SolicitarProgramacionTurnoPorGrupoTool.Mensajes.ResultadoProgramacionSolicitada);
        json["grupoResuelto"]!.GetValue<int>().Should().Be(0);
        json["programados"]!.AsArray().Should().BeEmpty();
        json["selector"]!.GetValue<string>().Should().Contain("area:cocina");
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_RechazaConElDominio_CuandoLaConsultaDeFichasFalla()
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        var (clienteSedes, _) = ClienteFalso.Con(SedeJson);
        var (clienteColaboradores, _) = ClienteFalso.Con("boom", HttpStatusCode.InternalServerError);
        var tool = new SolicitarProgramacionTurnoPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));

        var resultado = await tool.Run(
            null!, VentanaDesde, VentanaHasta, Turno, SedeProgramacion, null, "area:cocina",
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            SolicitarProgramacionTurnoPorGrupoTool.Mensajes.RechazoDelDominio, "boom"));
        programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }
}
