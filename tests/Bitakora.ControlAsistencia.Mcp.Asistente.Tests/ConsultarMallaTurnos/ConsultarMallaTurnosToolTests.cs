using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarMallaTurnos;

public class ConsultarMallaTurnosToolTests
{
    private static readonly RelojFalso Hoy = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static (ConsultarMallaTurnosTool Tool, HandlerEnlatado Malla, HandlerEnlatado Fichas)
        Armar(string jsonMalla, string? jsonFichas = null, HttpStatusCode status = HttpStatusCode.OK,
            HttpStatusCode statusFichas = HttpStatusCode.OK)
    {
        var (clienteMalla, handlerMalla) = ClienteFalso.Con(jsonMalla, status);
        var (clienteFichas, handlerFichas) = ClienteFalso.Con(jsonFichas ?? Leer("fichas-grupo.json"), statusFichas);
        var tool = new ConsultarMallaTurnosTool(
            new ControlHorasApi(clienteMalla),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            Hoy);
        return (tool, handlerMalla, handlerFichas);
    }

    private static Task<string> Ejecutar(
        ConsultarMallaTurnosTool tool, string? fecha = null, string? sede = null,
        string? etiquetas = null, string? codigos = null, string? cursor = null) =>
        tool.Run(null!, fecha, sede, etiquetas, codigos, cursor, TestContext.Current.CancellationToken);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarMallaTurnos", nombre);

    private static string MallaDe(int filas, string? cursor)
    {
        var malla = new
        {
            desde = "2026-10-12",
            hasta = "2026-10-18",
            anioIso = 2026,
            numeroSemana = 42,
            elementos = Enumerable.Range(1, filas).Select(i => new
            {
                codigoColaborador = $"C-{i:000}",
                casillas = Enumerable.Range(0, 7).Select(d => new
                {
                    fecha = new DateOnly(2026, 10, 12).AddDays(d).ToString("yyyy-MM-dd"),
                    tipo = "SinProgramar",
                    nombreTurno = (string?)null,
                    horarioResumido = (string?)null,
                    bloques = Array.Empty<object>(),
                    motivoAusencia = (string?)null
                })
            }),
            siguienteCursor = cursor
        };
        return JsonSerializer.Serialize(malla);
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ResuelveFichasAlLunesYEnviaCadaColaboradorConSuVigencia_CuandoSePasaSede()
    {
        var (tool, malla, fichas) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        var fichasBody = JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.AsObject();
        fichasBody["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        fichasBody["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
        fichasBody["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");

        malla.UltimaRequest!.Method.Method.Should().Be("QUERY");
        malla.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/control-horas/malla-semanal-turnos");
        var body = JsonNode.Parse(malla.UltimoCuerpoEnviado!)!.AsObject();
        var colaboradores = body["colaboradores"]!.AsArray().Select(c => c!.AsObject()).ToList();
        colaboradores.Select(c => c["codigoColaborador"]!.GetValue<string>()).Should().Equal("C-102", "C-103");
        colaboradores[0]["vigenteDesde"]!.GetValue<string>().Should().Be("2024-01-15");
        colaboradores[0]["vigenteHasta"].Should().BeNull();
        colaboradores[1]["vigenteDesde"]!.GetValue<string>().Should().Be("2024-02-01");
        colaboradores[1]["vigenteHasta"]!.GetValue<string>().Should().Be("2026-10-16");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ResuelveTodasLasFichasVigentes_CuandoNoSePasaGrupoNiCodigos()
    {
        var (tool, malla, fichas) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool, fecha: "2026-10-14");

        fichas.UltimaRequest.Should().NotBeNull("la malla siempre pasa por fichas");
        var fichasBody = JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!.AsObject();
        fichasBody["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        var body = JsonNode.Parse(malla.UltimoCuerpoEnviado!)!.AsObject();
        body["colaboradores"]!.AsArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task ConsultarMallaTurnos_IntersectaElGrupoConLosCodigos_CuandoSePasanSedeYCodigos()
    {
        var (tool, malla, _) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE", codigos: "C-103, C-999");

        var body = JsonNode.Parse(malla.UltimoCuerpoEnviado!)!.AsObject();
        body["colaboradores"]!.AsArray().Select(c => c!["codigoColaborador"]!.GetValue<string>())
            .Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_RespondeFraseSinLlamarALaMalla_CuandoLaInterseccionEstaVacia()
    {
        var (tool, malla, _) = Armar(Leer("malla-semana.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-14", codigos: "C-999");

        resultado.Should().NotStartWith("{").And.Contain("C-999").And.Contain("12 al 18");
        malla.UltimaRequest.Should().BeNull("sin sujetos no hay a quien consultar");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_RespondeFraseConCriterioYSemanaSinLlamarALaMalla_CuandoElGrupoNoTieneColaboradores()
    {
        var (tool, malla, _) = Armar(Leer("malla-semana.json"), """{"elementos":[]}""");

        var resultado = await Ejecutar(tool, fecha: "2026-10-14", sede: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("NORTE").And.Contain("12 al 18");
        malla.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_TraduceElRechazo_CuandoFallaLaLecturaDeFichas()
    {
        var (tool, malla, _) = Armar(
            Leer("malla-semana.json"), "Sede inexistente", statusFichas: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool, sede: "XX");

        resultado.Should().Be(string.Format(ConsultarMallaTurnosTool.Mensajes.RechazoDelDominio, "Sede inexistente"));
        malla.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_TraduceElRechazo_CuandoLaMallaDevuelve422()
    {
        var (tool, _, _) = Armar("El cursor no es valido", status: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(tool, cursor: "basura");

        resultado.Should().Be(string.Format(ConsultarMallaTurnosTool.Mensajes.RechazoDelDominio, "El cursor no es valido"));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ConsultaLaSemanaSiguiente_CuandoNoSePasaFecha()
    {
        var (tool, malla, fichas) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool);

        JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        var fecha = DateOnly.Parse(JsonNode.Parse(malla.UltimoCuerpoEnviado!)!["fecha"]!.GetValue<string>());
        fecha.Should().BeOnOrAfter(new DateOnly(2026, 10, 12)).And.BeOnOrBefore(new DateOnly(2026, 10, 18));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ConsultaLaSemanaDeLaFecha_CuandoSePasaFecha()
    {
        var (tool, malla, fichas) = Armar(Leer("malla-semana.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-07");

        JsonNode.Parse(fichas.UltimoCuerpoEnviado!)!["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-05");
        var fecha = DateOnly.Parse(JsonNode.Parse(malla.UltimoCuerpoEnviado!)!["fecha"]!.GetValue<string>());
        fecha.Should().BeOnOrAfter(new DateOnly(2026, 10, 5)).And.BeOnOrBefore(new DateOnly(2026, 10, 11));
        resultado.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_RespondeMensajeSinLlamarANadie_CuandoLaFechaEsInvalida()
    {
        var (tool, malla, fichas) = Armar("{}");

        var resultado = await Ejecutar(tool, fecha: "07/10/2026");

        resultado.Should().Be(string.Format(ConsultarMallaTurnosTool.Mensajes.FechaInvalida, "07/10/2026"));
        malla.UltimaRequest.Should().BeNull();
        fichas.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_RemodelaCadaColaboradorConSieteLineas_CuandoHayMalla()
    {
        var (tool, _, _) = Armar(Leer("malla-semana.json"));

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
        ana.Select(p => p.Key).Should().BeEquivalentTo(["codigo", "nombre", "dias"]);

        var dias = ana["dias"]!.AsArray().Select(d => d!.GetValue<string>()).ToList();
        dias.Should().HaveCount(7);
        dias[0].Should().StartWith("2026-10-12 lun: Manana 06:00-14:00 (").And.Contain("Norte").And.Contain("Sur");
        dias[1].Should().Be("2026-10-13 mar: descanso");
        dias[2].Should().Be("2026-10-14 mié: ausencia (Vacaciones)");
        dias[3].Should().Be("2026-10-15 jue: sin programar");
        dias[4].Should().Be("2026-10-16 vie: Tarde 14:00-22:00 (Norte)");
        dias[5].Should().Be("2026-10-17 sáb: sin programar");
        dias[6].Should().Be("2026-10-18 dom: fuera de vinculación");

        var luis = json["colaboradores"]![1]!.AsObject();
        luis["nombre"]!.GetValue<string>().Should().Be("Luis Gómez");
        luis["dias"]!.AsArray().Should().HaveCount(7).And.OnlyContain(d => d!.GetValue<string>().EndsWith("sin programar"));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_NoExponeIdsNiBloques_CuandoRemodelaLaMalla()
    {
        var (tool, _, _) = Armar(Leer("malla-semana.json"));

        var resultado = await Ejecutar(tool, fecha: "2026-10-14");

        resultado.Should().NotContain("S-1").And.NotContain("bloques").And.NotContain("sedeId");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_PideTakeIgualAlTope_CuandoConsultaLaMalla()
    {
        var (tool, malla, _) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool);

        JsonNode.Parse(malla.UltimoCuerpoEnviado!)!["take"]!.GetValue<int>().Should().Be(50);
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ReenviaElCursorTalCual_CuandoSeRecibeCursor()
    {
        var (tool, malla, _) = Armar(Leer("malla-semana.json"));

        await Ejecutar(tool, cursor: "opaco-123==");

        JsonNode.Parse(malla.UltimoCuerpoEnviado!)!["cursor"]!.GetValue<string>().Should().Be("opaco-123==");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraLos50RecibidosConCursorYNota_CuandoLaMallaTraeSiguienteCursor()
    {
        var (tool, _, _) = Armar(MallaDe(50, "cursor-de-la-malla"));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(50);
        json["colaboradores"]!.AsArray().Should().HaveCount(50);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-de-la-malla");
        json["nota"]!.GetValue<string>().Should().Be(string.Format(
            ConsultarMallaTurnosTool.Mensajes.NotaMasColaboradores, "cursor-de-la-malla"));
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_OmiteCursorYNota_CuandoLaMallaNoTraeSiguienteCursor()
    {
        var (tool, _, _) = Armar(MallaDe(2, null));

        var resultado = await Ejecutar(tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
    }
}
