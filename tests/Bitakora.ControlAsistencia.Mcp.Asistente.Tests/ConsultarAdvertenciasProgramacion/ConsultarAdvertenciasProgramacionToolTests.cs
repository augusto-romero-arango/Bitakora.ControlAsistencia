using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarAdvertenciasProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarAdvertenciasProgramacion;

public class ConsultarAdvertenciasProgramacionToolTests
{
    private static readonly RelojFalso Hoy = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static (ConsultarAdvertenciasProgramacionTool Tool, HandlerEnlatado Advertencias, HandlerEnlatado Fichas)
        Armar(string jsonAdvertencias, string jsonFichas = "[]", HttpStatusCode status = HttpStatusCode.OK)
    {
        var (clienteAdv, handlerAdv) = ClienteFalso.Con(jsonAdvertencias, status);
        var (clienteFichas, handlerFichas) = ClienteFalso.Con(jsonFichas);
        var tool = new ConsultarAdvertenciasProgramacionTool(
            new ControlHorasApi(clienteAdv),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            Hoy);
        return (tool, handlerAdv, handlerFichas);
    }

    private static Task<string> Ejecutar(
        ConsultarAdvertenciasProgramacionTool tool, string? fecha = null, string? sede = null,
        string? etiquetas = null, string? codigos = null, string? cursor = null) =>
        tool.Run(null!, fecha, sede, etiquetas, codigos, cursor, TestContext.Current.CancellationToken);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarAdvertenciasProgramacion", nombre);

    private static string ListaDe(int elementos, string? cursor)
    {
        var lista = new
        {
            desde = "2026-10-12",
            hasta = "2026-10-18",
            anioIso = 2026,
            numeroSemana = 42,
            elementos = Enumerable.Range(1, elementos).Select(i => new
            {
                codigoColaborador = $"C-{i:000}",
                nombreCompleto = $"Colaborador {i}",
                advertencias = new[] { new { tipo = "SuperaHorasSemanales", descripcion = "supera las horas semanales en 1 h" } },
                casillas = Array.Empty<object>()
            }),
            siguienteCursor = cursor
        };
        return JsonSerializer.Serialize(lista);
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_ConsultaLaSemanaSiguiente_CuandoNoSePasaFecha()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"));

        await Ejecutar(tool);

        adv.UltimaRequest!.Method.Method.Should().Be("QUERY");
        adv.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/control-horas/advertencias-programacion-semanal");
        var body = JsonNode.Parse(adv.UltimoCuerpoEnviado!)!.AsObject();
        DateOnly.Parse(body["fecha"]!.GetValue<string>()).Should().BeOnOrAfter(new DateOnly(2026, 10, 12))
            .And.BeOnOrBefore(new DateOnly(2026, 10, 18));
        body["soloConAdvertencias"]?.GetValue<bool>().Should().NotBe(false);
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_ConsultaLaSemanaDeLaFecha_CuandoSePasaFecha()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"));

        await Ejecutar(tool, fecha: "2026-10-07");

        var body = JsonNode.Parse(adv.UltimoCuerpoEnviado!)!.AsObject();
        DateOnly.Parse(body["fecha"]!.GetValue<string>()).Should().BeOnOrAfter(new DateOnly(2026, 10, 5))
            .And.BeOnOrBefore(new DateOnly(2026, 10, 11));
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_RespondeMensajeSinLlamarAlDominio_CuandoLaFechaEsInvalida()
    {
        var (tool, adv, fichas) = Armar("{}");

        var resultado = await Ejecutar(tool, fecha: "07/10/2026");

        resultado.Should().Be(string.Format(ConsultarAdvertenciasProgramacionTool.Mensajes.FechaInvalida, "07/10/2026"));
        adv.UltimaRequest.Should().BeNull();
        fichas.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_ResuelveElGrupoConElLunesYEnviaSusCodigos_CuandoSePasaSede()
    {
        var (tool, adv, fichas) = Armar(Leer("advertencias-semana.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        var fichasBody = JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.AsObject();
        fichasBody["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        fichasBody["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
        fichasBody["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");

        var body = JsonNode.Parse(adv.UltimoCuerpoEnviado!)!.AsObject();
        body["codigosColaborador"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_IntersectaElGrupoConLosCodigos_CuandoSePasanSedeYCodigos()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"), Leer("fichas-grupo.json"));

        await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE", codigos: "C-103, C-999");

        var body = JsonNode.Parse(adv.UltimoCuerpoEnviado!)!.AsObject();
        body["codigosColaborador"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_RespondeFraseSinLlamarAlDominio_CuandoElGrupoNoTieneColaboradores()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"), "[]");

        var resultado = await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        resultado.Should().Contain("NORTE");
        resultado.Should().NotStartWith("{");
        adv.UltimaRequest.Should().BeNull("sin sujetos no hay a quien consultar");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_RemodelaSoloAdvertencias_CuandoHayElementos()
    {
        var (tool, _, _) = Armar(Leer("advertencias-semana.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-14");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-10-12");
        json["hasta"]!.GetValue<string>().Should().Be("2026-10-18");
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("total").Should().BeFalse();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();

        var ana = json["colaboradores"]![0]!.AsObject();
        ana["codigo"]!.GetValue<string>().Should().Be("C-102");
        ana["nombre"]!.GetValue<string>().Should().Be("Ana Pérez");
        ana.Select(p => p.Key).Should().BeEquivalentTo(["codigo", "nombre", "advertencias"]);
        ana["advertencias"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal(
            "semana: supera las horas semanales en 6 h",
            "semana: le falta 1 día de descanso",
            "2026-10-14: supera el tope diario en 2 h");

        json["colaboradores"]![1]!["advertencias"]!.AsArray().Select(a => a!.GetValue<string>()).Should().Equal(
            "semana: le faltan 12 h para las horas semanales");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_RespondeFraseConLaSemanaYElCriterio_CuandoNadieTieneAdvertencias()
    {
        var (tool, _, _) = Armar(Leer("advertencias-semana-vacia.json"), Leer("fichas-grupo.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("12 al 18 de octubre de 2026").And.Contain("NORTE");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_PideTake51_CuandoConsultaElDominio()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"));

        await Ejecutar(tool);

        JsonNode.Parse(adv.UltimoCuerpoEnviado!)!["take"]!.GetValue<int>().Should().Be(51);
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_TopaEn50ConCursorYNota_CuandoLlegan51()
    {
        var (tool, _, _) = Armar(ListaDe(51, "cursor-del-dominio"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(50);
        json["colaboradores"]!.AsArray().Should().HaveCount(50);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-del-dominio");
        json["nota"]!.GetValue<string>().Should().Be(string.Format(
            ConsultarAdvertenciasProgramacionTool.Mensajes.NotaMasColaboradores, "cursor-del-dominio"));
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_OmiteCursorYNota_CuandoLlegan30()
    {
        var (tool, _, _) = Armar(ListaDe(30, null));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(30);
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_ReenviaElCursorTalCual_CuandoSeRecibeCursor()
    {
        var (tool, adv, _) = Armar(Leer("advertencias-semana.json"));

        await Ejecutar(tool, cursor: "opaco-123==");

        JsonNode.Parse(adv.UltimoCuerpoEnviado!)!["cursor"]!.GetValue<string>().Should().Be("opaco-123==");
    }

    [Fact]
    public async Task ConsultarAdvertenciasProgramacion_TraduceElRechazo_CuandoElDominioDevuelve422()
    {
        var (tool, _, _) = Armar("El cursor no es valido", status: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool, cursor: "basura");

        resultado.Should().Be(string.Format(
            ConsultarAdvertenciasProgramacionTool.Mensajes.RechazoDelDominio, "El cursor no es valido"));
    }
}
