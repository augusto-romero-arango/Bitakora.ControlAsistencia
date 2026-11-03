using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarBorradoresDeProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarBorradoresDeProgramacion;

public class ConsultarBorradoresDeProgramacionToolTests
{
    private static readonly RelojFalso Hoy = new(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static (ConsultarBorradoresDeProgramacionTool Tool, HandlerEnlatado Vista, HandlerEnlatado Fichas) Armar(
        string jsonVista, string jsonFichas = """{"elementos":[]}""", HttpStatusCode statusVista = HttpStatusCode.OK,
        HttpStatusCode statusFichas = HttpStatusCode.OK, TimeProvider? reloj = null)
    {
        var (clienteVista, vista) = ClienteFalso.Con(jsonVista, statusVista);
        var (clienteFichas, fichas) = ClienteFalso.Con(jsonFichas, statusFichas);
        var tool = new ConsultarBorradoresDeProgramacionTool(
            new ProgramacionApi(clienteVista),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            reloj ?? Hoy);
        return (tool, vista, fichas);
    }

    private static Task<string> Ejecutar(
        ConsultarBorradoresDeProgramacionTool tool, string? desde = null, string? hasta = null,
        string? sede = null, string? etiquetas = null, string? codigos = null, string? cursor = null) =>
        tool.Run(null!, desde, hasta, sede, etiquetas, codigos, cursor, TestContext.Current.CancellationToken);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarBorradoresDeProgramacion", nombre);

    private static string Vista(int colaboradores, string? cursor) =>
        new JsonObject
        {
            ["elementos"] = new JsonArray(Enumerable.Range(1, colaboradores).Select(i => (JsonNode)new JsonObject
            {
                ["codigoColaborador"] = $"C-{i:000}",
                ["identificacion"] = $"CC-{i:000}",
                ["nombreCompleto"] = $"Colaborador {i}",
                ["dias"] = new JsonArray(new JsonObject
                {
                    ["fecha"] = "2026-11-03", ["nombreTurno"] = "Tarde", ["horarioResumido"] = "14:00-22:00",
                    ["esHorarioAbierto"] = false, ["vencido"] = false, ["bloques"] = new JsonArray()
                })
            }).ToArray()),
            ["siguienteCursor"] = cursor
        }.ToJsonString();

    private static JsonObject Cuerpo(HandlerEnlatado h) => JsonNode.Parse(h.UltimoCuerpoEnviado!)!.AsObject();

    [Fact]
    public async Task ConsultarBorradores_LeeLaVistaConHoyYTake50SinPeriodo_CuandoNoHayFiltros()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool);

        vista.UltimaRequest!.Method.Method.Should().Be("QUERY");
        vista.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/programacion/borradores");
        var body = Cuerpo(vista);
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-11-03");
        body["take"]!.GetValue<int>().Should().Be(50);
        body.ContainsKey("desde").Should().BeFalse();
        body.ContainsKey("hasta").Should().BeFalse();
        (body["colaboradores"] is null || body["colaboradores"]!.AsArray().Count == 0).Should().BeTrue();
        JsonNode.Parse(resultado)!.AsObject().ContainsKey("desde").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarBorradores_ResuelveHoyEnBogota_CuandoEsMadrugadaEnUtc()
    {
        var reloj = new RelojFalso(new DateTimeOffset(2026, 11, 4, 3, 0, 0, TimeSpan.Zero));
        var (tool, vista, _) = Armar(Leer("borradores.json"), reloj: reloj);

        await Ejecutar(tool);

        Cuerpo(vista)["fechaReferencia"]!.GetValue<string>().Should().Be("2026-11-03");
    }

    [Fact]
    public async Task ConsultarBorradores_PasaDesdeYHastaALaVistaYLosEchaEnElSobre_CuandoHayPeriodo()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, "2026-11-01", "2026-11-30");

        var body = Cuerpo(vista);
        body["desde"]!.GetValue<string>().Should().Be("2026-11-01");
        body["hasta"]!.GetValue<string>().Should().Be("2026-11-30");
        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-11-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-11-30");
    }

    [Fact]
    public async Task ConsultarBorradores_RemodelaPorColaboradorConDiasEnTramos_CuandoHayPendientes()
    {
        var (tool, _, _) = Armar(Leer("borradores.json"));

        var json = JsonNode.Parse(await Ejecutar(tool))!.AsObject();

        json.ContainsKey("total").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        var ana = json["colaboradores"]![0]!.AsObject();
        ana["codigo"]!.GetValue<string>().Should().Be("C-102");
        ana["identificacion"]!.GetValue<string>().Should().Be("CC-1020");
        ana["nombre"]!.GetValue<string>().Should().Be("Ana Pérez");
        ana["dias"]!.AsArray().Select(d => d!.GetValue<string>()).Should()
            .Equal("2026-11-03 a 2026-11-05", "2026-11-10");
        ana.ContainsKey("vencido").Should().BeFalse();
        ana.ContainsKey("nombreTurno").Should().BeFalse();
        json["colaboradores"]![1]!["dias"]!.AsArray().Select(d => d!.GetValue<string>()).Should()
            .Equal("2026-11-12");
    }

    [Fact]
    public async Task ConsultarBorradores_ResuelveElGrupoAHoyYLeeLaVistaConSusCodigos_CuandoSeFiltraPorSede()
    {
        var (tool, vista, fichas) = Armar(Leer("borradores.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, sede: "NORTE");

        (fichas.UltimaRequest!.RequestUri!.ToString() + fichas.UltimoCuerpoEnviado).Should().Contain("2026-11-03");
        Cuerpo(vista)["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should()
            .Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarBorradores_ResuelveElGrupoADesde_CuandoHayPeriodoYSede()
    {
        var (tool, _, fichas) = Armar(Leer("borradores.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, "2026-11-09", "2026-11-30", sede: "NORTE");

        (fichas.UltimaRequest!.RequestUri!.ToString() + fichas.UltimoCuerpoEnviado).Should().Contain("2026-11-09");
    }

    [Fact]
    public async Task ConsultarBorradores_IntersectaCodigosConElGrupo_CuandoSeCombinanSedeYCodigos()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, sede: "NORTE", codigos: "C-103, C-999");

        Cuerpo(vista)["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarBorradores_UsaLosCodigosSinLeerFichas_CuandoSoloSePidenCodigos()
    {
        var (tool, vista, fichas) = Armar(Leer("borradores.json"));

        await Ejecutar(tool, codigos: "C-102, C-103");

        fichas.UltimaRequest.Should().BeNull();
        Cuerpo(vista)["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should()
            .Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarBorradores_RespondeGrupoVacioSinLeerLaVista_CuandoNingunColaboradorCumpleElCriterio()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, sede: "NORTE");

        resultado.Should().Be(string.Format(
            ConsultarBorradoresDeProgramacionTool.Mensajes.GrupoSinColaboradores,
            string.Format(ConsultarBorradoresDeProgramacionTool.Mensajes.CriterioSede, "NORTE")));
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_ReenviaElCursorALaVista_CuandoSePideLaSiguientePagina()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        await Ejecutar(tool, cursor: "abc123==");

        Cuerpo(vista)["cursor"]!.GetValue<string>().Should().Be("abc123==");
    }

    [Fact]
    public async Task ConsultarBorradores_ReenviaSiguienteCursorConNota_CuandoLaVistaTienePaginaSiguiente()
    {
        var (tool, _, _) = Armar(Vista(50, "cursor-siguiente=="));

        var json = JsonNode.Parse(await Ejecutar(tool))!.AsObject();

        json["mostrando"]!.GetValue<int>().Should().Be(50);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-siguiente==");
        json["nota"]!.GetValue<string>().Should().Be(ConsultarBorradoresDeProgramacionTool.Mensajes.NotaMasColaboradores);
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarBorradores_RespondeFraseDeVacioConElPeriodo_CuandoNoHayElementosConPeriodo()
    {
        var (tool, _, _) = Armar(Leer("borradores-vacio.json"));

        var resultado = await Ejecutar(tool, "2026-11-01", "2026-11-30");

        resultado.Should().StartWith("No hay borrador de programacion pendiente")
            .And.Contain("2026-11-01").And.Contain("2026-11-30");
    }

    [Fact]
    public async Task ConsultarBorradores_RespondeFraseDeVacioConElCriterio_CuandoNoHayElementosConCodigos()
    {
        var (tool, _, _) = Armar(Leer("borradores-vacio.json"));

        var resultado = await Ejecutar(tool, codigos: "C-102");

        resultado.Should().StartWith("No hay borrador de programacion pendiente")
            .And.Contain(string.Format(ConsultarBorradoresDeProgramacionTool.Mensajes.CriterioCodigos, "C-102"));
    }

    [Fact]
    public async Task ConsultarBorradores_RechazaSinLeerLaVista_CuandoSoloLlegaDesde()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, desde: "2026-11-01");

        resultado.Should().Be(ConsultarBorradoresDeProgramacionTool.Mensajes.RangoIncompleto);
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_RechazaSinLeerLaVista_CuandoSoloLlegaHasta()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, hasta: "2026-11-30");

        resultado.Should().Be(ConsultarBorradoresDeProgramacionTool.Mensajes.RangoIncompleto);
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_RechazaSinLeerLaVista_CuandoLaFechaEstaMalFormada()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, "2026-99-99", "2026-11-30");

        resultado.Should().Be(string.Format(
            ConsultarBorradoresDeProgramacionTool.Mensajes.FechaInvalida, "desde", "2026-99-99"));
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_RechazaSinLeerLaVista_CuandoHastaEstaMalFormada()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, "2026-11-01", "treinta");

        resultado.Should().Be(string.Format(
            ConsultarBorradoresDeProgramacionTool.Mensajes.FechaInvalida, "hasta", "treinta"));
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_RechazaSinLeerLaVista_CuandoDesdeEsPosteriorAHasta()
    {
        var (tool, vista, _) = Armar(Leer("borradores.json"));

        var resultado = await Ejecutar(tool, "2026-11-30", "2026-11-01");

        resultado.Should().Be(ConsultarBorradoresDeProgramacionTool.Mensajes.DesdePosteriorAHasta);
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarBorradores_DevuelveElRechazoComoTexto_CuandoLaVistaRespondeNo2xx()
    {
        var (tool, _, _) = Armar("cursor invalido", statusVista: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool, cursor: "basura");

        resultado.Should().StartWith(string.Format(
            ConsultarBorradoresDeProgramacionTool.Mensajes.RechazoDelDominio, "").TrimEnd())
            .And.Contain("cursor invalido");
    }

    [Fact]
    public async Task ConsultarBorradores_DevuelveElRechazoSinLeerLaVista_CuandoLasFichasRespondenNo2xx()
    {
        var (tool, vista, _) = Armar(
            Leer("borradores.json"), "fichas caidas", statusFichas: HttpStatusCode.InternalServerError);

        var resultado = await Ejecutar(tool, sede: "NORTE");

        resultado.Should().StartWith(string.Format(
            ConsultarBorradoresDeProgramacionTool.Mensajes.RechazoDelDominio, "").TrimEnd())
            .And.Contain("fichas caidas");
        vista.UltimaRequest.Should().BeNull();
    }
}
