using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAusencias;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarAusencias;

public class ConsultarAusenciasToolTests
{
    private static ConsultarAusenciasTool Tool(HttpClient cliente) =>
        new(new ProgramacionApi(cliente),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(ClienteFalso.Con("""{"elementos":[]}""").Cliente)));

    private static (ConsultarAusenciasTool Tool, HandlerEnlatado Ausencias, HandlerEnlatado Fichas) Armar(
        string jsonAusencias, string jsonFichas = """{"elementos":[]}""")
    {
        var (clienteAus, handlerAus) = ClienteFalso.Con(jsonAusencias);
        var (clienteFichas, handlerFichas) = ClienteFalso.Con(jsonFichas);
        var tool = new ConsultarAusenciasTool(
            new ProgramacionApi(clienteAus),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)));
        return (tool, handlerAus, handlerFichas);
    }

    private static Task<string> Ejecutar(
        ConsultarAusenciasTool tool, string desde = "2026-10-12", string hasta = "2026-10-18",
        string? codigos = null, string? sede = null, string? etiquetas = null, string? cursor = null) =>
        tool.Run(null!, desde, hasta, codigos, sede, etiquetas, cursor, TestContext.Current.CancellationToken);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarAusencias", nombre);

    private static string ListaDe(int colaboradores, string? cursor) =>
        JsonSerializer.Serialize(new
        {
            desde = "2026-10-12",
            hasta = "2026-10-18",
            rangoRecortado = false,
            elementos = Enumerable.Range(1, colaboradores).Select(i => new
            {
                codigoColaborador = $"C-{i:000}",
                nombreCompleto = $"Colaborador {i}",
                ausencias = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(),
                        motivo = "Vacaciones",
                        tramos = new[] { new { desde = "2026-10-12", hasta = "2026-10-13" } }
                    }
                }
            }),
            siguienteCursor = cursor
        });

    [Fact]
    public async Task ConsultarAusencias_EnviaElQueryConElFiltro_CuandoLasFechasSonValidas()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo.json"));

        await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", "COL-1, COL-2", null, null, null,
            TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/programacion/ausencias");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["desde"]!.GetValue<string>().Should().Be("2026-10-12");
        body["hasta"]!.GetValue<string>().Should().Be("2026-10-18");
        body["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should()
            .Equal("COL-1", "COL-2");
    }

    [Fact]
    public async Task ConsultarAusencias_OmiteElFiltroDeColaboradores_CuandoNoSePasanCodigos()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo.json"));

        await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", "  ", null, null, null,
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        (body["colaboradores"] is null || body["colaboradores"]!.AsArray().Count == 0).Should().BeTrue();
    }

    [Fact]
    public async Task ConsultarAusencias_RemodelaPorColaborador_CuandoHayAusentes()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null, null, null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-10-12");
        json["hasta"]!.GetValue<string>().Should().Be("2026-10-18");
        json.ContainsKey("total").Should().BeFalse();
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("nota").Should().BeFalse("sin recorte ni mas paginas no hay senal");

        var ana = json["colaboradores"]![0]!.AsObject();
        ana["codigo"]!.GetValue<string>().Should().Be("COL-1");
        ana["nombre"]!.GetValue<string>().Should().Be("Ana Perez");
        ana["ausencias"]!.AsArray().Should().HaveCount(2);
        ana["ausencias"]![0]!["motivo"]!.GetValue<string>().Should().Be("Vacaciones");
        ana["ausencias"]![0]!["tramos"]!.AsArray().Select(t => t!.GetValue<string>()).Should()
            .Equal("2026-10-12 a 2026-10-14");
        ana["ausencias"]![0]!.AsObject().ContainsKey("id").Should().BeFalse("el id es interno");
        ana["ausencias"]![1]!["motivo"]!.GetValue<string>().Should().Be("IncapacidadMedica");
        ana["ausencias"]![1]!["tramos"]!.AsArray().Select(t => t!.GetValue<string>()).Should()
            .Equal("2026-10-16", "2026-10-18");

        json["colaboradores"]![1]!["codigo"]!.GetValue<string>().Should().Be("COL-2");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeQueNadieFalta_CuandoNoHayAusentes()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo-vacio.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null, null, null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.NadieFalta, "2026-10-12", "2026-10-18", ""));
    }

    [Fact]
    public async Task ConsultarAusencias_SenalaElRecorteConElPeriodoAplicado_CuandoElDominioRecortoElRango()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo-recortado.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2027-03-31", null, null, null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-10-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-11-04");
        json["nota"]!.GetValue<string>().Should().Be(
            "El periodo pedido excedia 35 dias y fue recortado; periodo aplicado: 2026-10-01 a 2026-11-04.");
        json["colaboradores"]![0]!["ausencias"]![0]!["tramos"]![0]!.GetValue<string>()
            .Should().Be("2026-10-05 a 2026-10-09");
    }

    [Fact]
    public async Task ConsultarAusencias_NoInventaRecorte_CuandoElDominioAplica35DiasSinRecorte()
    {
        var respuesta = JsonNode.Parse(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo-recortado.json"))!;
        respuesta["rangoRecortado"] = false;
        var (cliente, handler) = ClienteFalso.Con(respuesta.ToJsonString());

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2026-11-04", null, null, null, null,
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["desde"]!.GetValue<string>().Should().Be("2026-10-01");
        body["hasta"]!.GetValue<string>().Should().Be("2026-11-04");
        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-10-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-11-04");
        json.AsObject().ContainsKey("nota").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAusencias_DelegaElRecorteAlDominio_CuandoSePiden36Dias()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ConsultarAusencias", "ausencias-equipo-recortado.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2026-11-05", null, null, null, null,
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!;
        body["desde"]!.GetValue<string>().Should().Be("2026-10-01");
        body["hasta"]!.GetValue<string>().Should().Be("2026-11-05");
        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-10-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-11-04");
        json["nota"]!.GetValue<string>().Should().Be(
            "El periodo pedido excedia 35 dias y fue recortado; periodo aplicado: 2026-10-01 a 2026-11-04.");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeMensajeSinLlamarAlDominio_CuandoUnaFechaEsInvalida()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "12/10/2026", "2026-10-18", null, null, null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.FechaInvalida, "desde", "12/10/2026"));
        handler.UltimaRequest.Should().BeNull("la validacion es previa a la llamada HTTP");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeMensajeSinLlamarAlDominio_CuandoHastaEstaAusente()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", null!, null, null, null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Contain("hasta");
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeMensajeSinLlamarAlDominio_CuandoDesdeEsPosteriorAHasta()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "2026-10-18", "2026-10-12", null, null, null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(ConsultarAusenciasTool.Mensajes.DesdePosteriorAHasta);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_TraduceElRechazo_CuandoElDominioDevuelve422()
    {
        var (cliente, _) = ClienteFalso.Con(
            "Desde y Hasta son obligatorios", HttpStatusCode.UnprocessableEntity);

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null, null, null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.RechazoDelDominio, "Desde y Hasta son obligatorios"));
    }

    [Fact]
    public async Task ConsultarAusencias_ResuelveElGrupoConDesdeYEnviaSusCodigos_CuandoSePasaSede()
    {
        var (tool, aus, fichas) = Armar(Leer("ausencias-equipo.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, sede: "NORTE");

        var fichasBody = JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.AsObject();
        fichasBody["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        fichasBody["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
        fichasBody["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");

        var body = JsonNode.Parse(aus.UltimoCuerpoEnviado!)!.AsObject();
        body["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarAusencias_ResuelveElGrupoPorEtiquetas_CuandoSePasanEtiquetas()
    {
        var (tool, aus, fichas) = Armar(Leer("ausencias-equipo.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, etiquetas: "area:ventas");

        fichas.UltimaRequest.Should().NotBeNull();
        JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.ToJsonString().Should().Contain("ventas");
        JsonNode.Parse(aus.UltimoCuerpoEnviado!)!["colaboradores"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsultarAusencias_IntersectaElGrupoConLosCodigos_CuandoSePasanSedeYCodigos()
    {
        var (tool, aus, _) = Armar(Leer("ausencias-equipo.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, sede: "NORTE", codigos: "C-103, C-999");

        var body = JsonNode.Parse(aus.UltimoCuerpoEnviado!)!.AsObject();
        body["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarAusencias_NoConsultaFichas_CuandoSoloSePasanCodigos()
    {
        var (tool, aus, fichas) = Armar(Leer("ausencias-equipo.json"));

        await Ejecutar(tool, codigos: "COL-1");

        fichas.UltimaRequest.Should().BeNull();
        JsonNode.Parse(aus.UltimoCuerpoEnviado!)!["colaboradores"]!.AsArray()
            .Select(c => c!.GetValue<string>()).Should().Equal("COL-1");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeFraseSinLlamarAlEndpointDeAusencias_CuandoElGrupoEstaVacio()
    {
        var (tool, aus, _) = Armar(Leer("ausencias-equipo.json"), """{"elementos":[]}""");

        var resultado = await Ejecutar(tool, sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("NORTE").And.Contain("2026-10-12").And.Contain("2026-10-18");
        aus.UltimaRequest.Should().BeNull("una lista vacia significaria sin filtro y traeria a todos");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeFraseSinLlamarAlEndpointDeAusencias_CuandoLaInterseccionEstaVacia()
    {
        var (tool, aus, _) = Armar(Leer("ausencias-equipo.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, sede: "NORTE", codigos: "C-999");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("NORTE").And.Contain("C-999");
        aus.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_TraduceElRechazoSinSeguir_CuandoFallaLaLecturaDeFichas()
    {
        var (clienteAus, aus) = ClienteFalso.Con(Leer("ausencias-equipo.json"));
        var (clienteFichas, _) = ClienteFalso.Con("Sede inexistente", HttpStatusCode.UnprocessableEntity);
        var tool = new ConsultarAusenciasTool(
            new ProgramacionApi(clienteAus),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)));

        var resultado = await Ejecutar(tool, sede: "NORTE");

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.RechazoDelDominio, "Sede inexistente"));
        aus.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_PideTakeIgualAlTope_CuandoConsultaElDominio()
    {
        var (tool, aus, _) = Armar(Leer("ausencias-equipo.json"));

        await Ejecutar(tool);

        JsonNode.Parse(aus.UltimoCuerpoEnviado!)!["take"]!.GetValue<int>().Should().Be(50);
    }

    [Fact]
    public async Task ConsultarAusencias_ReenviaElCursorTalCual_CuandoSeRecibeCursor()
    {
        var (tool, aus, _) = Armar(Leer("ausencias-equipo.json"));

        await Ejecutar(tool, cursor: "opaco-123==");

        JsonNode.Parse(aus.UltimoCuerpoEnviado!)!["cursor"]!.GetValue<string>().Should().Be("opaco-123==");
    }

    [Fact]
    public async Task ConsultarAusencias_MuestraLos50RecibidosConCursorYNota_CuandoElSobreTraeSiguienteCursor()
    {
        var (tool, _, _) = Armar(ListaDe(50, "cursor-del-dominio"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(50);
        json["colaboradores"]!.AsArray().Should().HaveCount(50);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-del-dominio");
        json["nota"]!.GetValue<string>().Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.NotaMasColaboradores, "cursor-del-dominio"));
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAusencias_OmiteCursorYNota_CuandoElBackendNoDevuelveCursor()
    {
        var (tool, _, _) = Armar(ListaDe(30, null));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(30);
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAusencias_UneNotaDeRecorteYDeMas_CuandoHayAmbas()
    {
        var respuesta = JsonNode.Parse(Leer("ausencias-equipo-recortado.json"))!;
        respuesta["siguienteCursor"] = "cursor-del-dominio";
        var (tool, _, _) = Armar(respuesta.ToJsonString());

        var resultado = await Ejecutar(tool, desde: "2026-10-01", hasta: "2027-03-31");

        var nota = JsonNode.Parse(resultado)!["nota"]!.GetValue<string>();
        nota.Should().Contain(string.Format(ConsultarAusenciasTool.Mensajes.NotaRecorte, "2026-10-01", "2026-11-04"));
        nota.Should().Contain(string.Format(ConsultarAusenciasTool.Mensajes.NotaMasColaboradores, "cursor-del-dominio"));
    }

    [Fact]
    public async Task ConsultarAusencias_IncluyeElCriterioEnLaFrase_CuandoNadieFaltaConSede()
    {
        var (tool, _, _) = Armar(Leer("ausencias-equipo-vacio.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("2026-10-12").And.Contain("2026-10-18").And.Contain("NORTE");
    }
}
