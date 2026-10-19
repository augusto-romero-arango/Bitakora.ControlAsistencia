using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarDiasSinProgramar;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarDiasSinProgramar;

public class ConsultarDiasSinProgramarToolTests
{
    private static readonly RelojFalso Hoy = new(new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static (ConsultarDiasSinProgramarTool Tool, HandlerEnlatado Dias, HandlerEnlatado Fichas)
        Armar(string jsonDias, string jsonFichas = """{"elementos":[]}""", HttpStatusCode status = HttpStatusCode.OK,
            HttpStatusCode statusFichas = HttpStatusCode.OK)
    {
        var (clienteDias, handlerDias) = ClienteFalso.Con(jsonDias, status);
        var (clienteFichas, handlerFichas) = ClienteFalso.Con(jsonFichas, statusFichas);
        var tool = new ConsultarDiasSinProgramarTool(
            new ControlHorasApi(clienteDias),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            Hoy);
        return (tool, handlerDias, handlerFichas);
    }

    private static Task<string> Ejecutar(
        ConsultarDiasSinProgramarTool tool, string? fecha = null, int? semanas = null, string? sede = null,
        string? etiquetas = null, string? codigos = null, string? cursor = null) =>
        tool.Run(null!, fecha, semanas, sede, etiquetas, codigos, cursor, TestContext.Current.CancellationToken);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarDiasSinProgramar", nombre);

    private static string ListaDe(int elementos, string? cursor) =>
        JsonSerializer.Serialize(new
        {
            desde = "2026-10-19",
            hasta = "2026-10-25",
            rangoRecortado = false,
            elementos = Enumerable.Range(1, elementos).Select(i => new
            {
                codigoColaborador = $"C-{i:000}",
                diasSinProgramar = new[] { "2026-10-21" }
            }),
            siguienteCursor = cursor
        });

    [Fact]
    public async Task ConsultarDiasSinProgramar_ConsultaLaSemanaSiguiente_CuandoNoSePasaFechaNiSemanas()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool);

        dias.UltimaRequest!.Method.Method.Should().Be("QUERY");
        dias.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/control-horas/dias-sin-programar");
        var body = JsonNode.Parse(dias.UltimoCuerpoEnviado!)!.AsObject();
        body["desdeFecha"]!.GetValue<string>().Should().Be("2026-10-19");
        body["hastaFecha"]!.GetValue<string>().Should().Be("2026-10-25");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_AmpliaLaVentanaALasSemanasPedidas_CuandoSePasanFechaYSemanas()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, fecha: "2026-10-14", semanas: 3);

        var body = JsonNode.Parse(dias.UltimoCuerpoEnviado!)!.AsObject();
        body["desdeFecha"]!.GetValue<string>().Should().Be("2026-10-12");
        body["hastaFecha"]!.GetValue<string>().Should().Be("2026-11-01");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeMensajeSinLlamarANadie_CuandoLaFechaEsInvalida()
    {
        var (tool, dias, fichas) = Armar("{}");

        var resultado = await Ejecutar(tool, fecha: "14/10/2026");

        resultado.Should().Be(string.Format(ConsultarDiasSinProgramarTool.Mensajes.FechaInvalida, "14/10/2026"));
        dias.UltimaRequest.Should().BeNull();
        fichas.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeMensajeSinLlamarANadie_CuandoSemanasEsCero()
    {
        var (tool, dias, fichas) = Armar("{}");

        var resultado = await Ejecutar(tool, semanas: 0);

        resultado.Should().Be(string.Format(ConsultarDiasSinProgramarTool.Mensajes.SemanasFueraDeRango, 0));
        dias.UltimaRequest.Should().BeNull();
        fichas.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeMensajeSinLlamarANadie_CuandoSemanasEsSeis()
    {
        var (tool, dias, fichas) = Armar("{}");

        var resultado = await Ejecutar(tool, semanas: 6);

        resultado.Should().Be(string.Format(ConsultarDiasSinProgramarTool.Mensajes.SemanasFueraDeRango, 6));
        dias.UltimaRequest.Should().BeNull();
        fichas.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_ResuelveFichasDeTodosConElLunesSinTake_CuandoNoHayFiltros()
    {
        var (tool, _, fichas) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, fecha: "2026-10-14");

        var fichasBody = JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.AsObject();
        fichasBody["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        fichasBody["codigoSede"].Should().BeNull();
        fichasBody["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_FiltraLasFichasPorSede_CuandoSePasaSede()
    {
        var (tool, _, fichas) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_EnviaCadaColaboradorConSuVigencia_CuandoHayFichas()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool);

        var colaboradores = JsonNode.Parse(dias.UltimoCuerpoEnviado!)!["colaboradores"]!.AsArray();
        colaboradores.Should().HaveCount(2);
        colaboradores[0]!["codigoColaborador"]!.GetValue<string>().Should().Be("C-102");
        colaboradores[0]!["vigenteDesde"]!.GetValue<string>().Should().Be("2024-01-15");
        colaboradores[0]!["vigenteHasta"].Should().BeNull();
        colaboradores[1]!["codigoColaborador"]!.GetValue<string>().Should().Be("C-103");
        colaboradores[1]!["vigenteHasta"]!.GetValue<string>().Should().Be("2026-12-31");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_IntersectaElGrupoConLosCodigos_CuandoSePasanCodigos()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, codigos: "c-103, C-999");

        var colaboradores = JsonNode.Parse(dias.UltimoCuerpoEnviado!)!["colaboradores"]!.AsArray();
        colaboradores.Select(c => c!["codigoColaborador"]!.GetValue<string>()).Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeFraseSinLlamarAControlHoras_CuandoElGrupoNoTieneColaboradores()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), """{"elementos":[]}""");

        var resultado = await Ejecutar(tool, fecha: "2026-10-19", semanas: 3, sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("NORTE").And.Contain("19 de octubre al 8 de noviembre de 2026");
        dias.UltimaRequest.Should().BeNull("sin sujetos no hay a quien consultar");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_NombraATodosLosColaboradoresComoCriterio_CuandoNoHayFiltrosNiFichasVigentes()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), """{"elementos":[]}""");

        var resultado = await Ejecutar(tool, fecha: "2026-10-19");

        resultado.Should().Be(string.Format(
            ConsultarDiasSinProgramarTool.Mensajes.GrupoSinColaboradores,
            ConsultarDiasSinProgramarTool.Mensajes.CriterioTodos,
            "19 al 25 de octubre de 2026"));
        dias.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_DescribeElPeriodoConAmbosAnios_CuandoLaVentanaCruzaDeAnio()
    {
        var (tool, _, _) = Armar(Leer("dias-sin-programar-vacia.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-12-30", semanas: 2);

        resultado.Should().Contain("28 de diciembre de 2026 al 10 de enero de 2027");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeFraseSinLlamarAControlHoras_CuandoLaInterseccionEsVacia()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, codigos: "C-999");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("C-999");
        dias.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_TraduceElRechazo_CuandoFallaLaLecturaDeFichas()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), "ficha rota", statusFichas: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool);

        resultado.Should().Be(string.Format(ConsultarDiasSinProgramarTool.Mensajes.RechazoDelDominio, "ficha rota"));
        dias.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_TraduceElRechazo_CuandoElEndpointDevuelve422()
    {
        var (tool, _, _) = Armar("El cursor no es valido", Leer("fichas-grupo.json"), HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool, cursor: "basura");

        resultado.Should().Be(string.Format(ConsultarDiasSinProgramarTool.Mensajes.RechazoDelDominio, "El cursor no es valido"));
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_CompactaLosDiasEnTramosYTomaElNombreDeLaFicha_CuandoHayElementos()
    {
        var (tool, _, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-10-19");
        json["hasta"]!.GetValue<string>().Should().Be("2026-11-08");
        json["mostrando"]!.GetValue<int>().Should().Be(1);
        json.ContainsKey("total").Should().BeFalse();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();

        var ana = json["colaboradores"]![0]!.AsObject();
        ana.Select(p => p.Key).Should().BeEquivalentTo(["codigo", "nombre", "diasSinProgramar"]);
        ana["codigo"]!.GetValue<string>().Should().Be("C-102");
        ana["nombre"]!.GetValue<string>().Should().Be("Ana Pérez");
        ana["diasSinProgramar"]!.AsArray().Select(d => d!.GetValue<string>()).Should().Equal(
            "2026-10-21 a 2026-10-25", "2026-11-02");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_RespondeFraseConPeriodoYCriterio_CuandoNadieTieneDiasSinProgramar()
    {
        var (tool, _, _) = Armar(Leer("dias-sin-programar-vacia.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-19", semanas: 3, sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("19 de octubre al 8 de noviembre de 2026").And.Contain("NORTE");
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_PideTakeIgualAlTope_CuandoConsultaControlHoras()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool);

        JsonNode.Parse(dias.UltimoCuerpoEnviado!)!["take"]!.GetValue<int>().Should().Be(50);
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_ReenviaCursorYNota_CuandoElEndpointTraeSiguienteCursor()
    {
        var (tool, _, _) = Armar(ListaDe(2, "cursor-del-dominio"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-del-dominio");
        json["nota"]!.GetValue<string>().Should().Be(string.Format(
            ConsultarDiasSinProgramarTool.Mensajes.NotaMasColaboradores, "cursor-del-dominio"));
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_OmiteCursorYNota_CuandoElEndpointNoTraeSiguienteCursor()
    {
        var (tool, _, _) = Armar(ListaDe(2, null), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarDiasSinProgramar_ReenviaElCursorTalCualConLaListaCompleta_CuandoSeRecibeCursor()
    {
        var (tool, dias, _) = Armar(Leer("dias-sin-programar.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, cursor: "opaco-123==");

        var body = JsonNode.Parse(dias.UltimoCuerpoEnviado!)!.AsObject();
        body["cursor"]!.GetValue<string>().Should().Be("opaco-123==");
        body["colaboradores"]!.AsArray().Should().HaveCount(2);
    }
}
