using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarProgramacion;

public class ConsultarProgramacionToolTests
{
    private const string SinFichas = """{"elementos":[]}""";

    private sealed record Fakes(
        ConsultarProgramacionTool Tool, HandlerEnlatado Turnos, HandlerEnlatado Fichas, HandlerEnlatado Sedes);

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarProgramacion", nombre);

    private static Fakes Armar(
        string? turnos = null,
        string jsonFichas = SinFichas,
        string? jsonSede = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusFichas = HttpStatusCode.OK,
        HttpStatusCode statusSede = HttpStatusCode.OK)
    {
        var (clienteTurnos, handlerTurnos) = ClienteFalso.Con(turnos ?? Leer("turnos-vigentes.json"), statusTurnos);
        var (clienteFichas, handlerFichas) = ClienteFalso.Con(jsonFichas, statusFichas);
        var (clienteSedes, handlerSedes) = ClienteFalso.Con(jsonSede ?? Leer("ficha-sede.json"), statusSede);
        var tool = new ConsultarProgramacionTool(
            new ControlHorasApi(clienteTurnos),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)));
        return new Fakes(tool, handlerTurnos, handlerFichas, handlerSedes);
    }

    private static Task<string> Ejecutar(
        ConsultarProgramacionTool tool,
        string desde = "2026-07-01",
        string hasta = "2026-07-31",
        string? codigos = null,
        string? sede = null,
        string? etiquetas = null,
        string? sedeTrabajo = null,
        string? cursor = null) =>
        tool.Run(null!, desde, hasta, codigos, sede, etiquetas, sedeTrabajo, cursor,
            TestContext.Current.CancellationToken);

    private static JsonObject BodyDe(HandlerEnlatado handler) =>
        JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();

    private static string[] Codigos(JsonObject body) =>
        [.. body["codigosColaborador"]!.AsArray().Select(c => c!.GetValue<string>())];

    private static string ConCursor(string json, string cursor)
    {
        var nodo = JsonNode.Parse(json)!.AsObject();
        nodo["siguienteCursor"] = cursor;
        return nodo.ToJsonString();
    }

    [Fact]
    public async Task ConsultarProgramacion_EnviaElQueryConTakeCincuentaYSinFiltros_CuandoSoloVienenLasFechas()
    {
        var fakes = Armar();

        await Ejecutar(fakes.Tool);

        fakes.Turnos.UltimaRequest!.Method.Method.Should().Be("QUERY");
        fakes.Turnos.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/control-horas/turnos-vigentes");
        var body = BodyDe(fakes.Turnos);
        body["desdeFecha"]!.GetValue<string>().Should().Be("2026-07-01");
        body["hastaFecha"]!.GetValue<string>().Should().Be("2026-07-31");
        body["take"]!.GetValue<int>().Should().Be(50);
        body["codigosColaborador"].Should().BeNull("sin sujeto es el panorama de todos");
        body["sedeId"].Should().BeNull();
        body["cursor"].Should().BeNull();
        fakes.Fichas.UltimaRequest.Should().BeNull("sin sede ni etiquetas no se resuelve grupo");
        fakes.Sedes.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_ReenviaElCursorTalCual_CuandoLlegaCursor()
    {
        var fakes = Armar();

        await Ejecutar(fakes.Tool, cursor: "abc==/opaco");

        BodyDe(fakes.Turnos)["cursor"]!.GetValue<string>().Should().Be("abc==/opaco");
    }

    [Fact]
    public async Task ConsultarProgramacion_EnviaLosCodigosTalCual_CuandoSoloVienenCodigosColaborador()
    {
        var fakes = Armar();

        await Ejecutar(fakes.Tool, codigos: "COL-1, COL-2");

        Codigos(BodyDe(fakes.Turnos)).Should().Equal("COL-1", "COL-2");
        fakes.Fichas.UltimaRequest.Should().BeNull("los codigos explicitos no requieren resolver grupo");
    }

    [Fact]
    public async Task ConsultarProgramacion_ResuelveElGrupoConDesdeYEnviaSusCodigos_CuandoSePasaSede()
    {
        var fakes = Armar(jsonFichas: Fixtures.Leer("ConsultarAdvertenciasProgramacion", "fichas-grupo.json"));

        await Ejecutar(fakes.Tool, desde: "2026-07-06", sede: "NORTE");

        var fichas = BodyDe(fakes.Fichas);
        fichas["fechaReferencia"]!.GetValue<string>().Should().Be("2026-07-06");
        fichas["codigoSede"]!.GetValue<string>().Should().Be("NORTE");
        fichas["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");
        Codigos(BodyDe(fakes.Turnos)).Should().Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarProgramacion_ResuelveElGrupoYEnviaSusCodigos_CuandoSePasanEtiquetas()
    {
        var fakes = Armar(jsonFichas: Fixtures.Leer("ConsultarAdvertenciasProgramacion", "fichas-grupo.json"));

        await Ejecutar(fakes.Tool, etiquetas: "turno:noche");

        fakes.Fichas.UltimaRequest.Should().NotBeNull();
        Codigos(BodyDe(fakes.Turnos)).Should().Equal("C-102", "C-103");
    }

    [Fact]
    public async Task ConsultarProgramacion_EnviaSoloLaInterseccion_CuandoSePasanSedeYCodigos()
    {
        var fakes = Armar(jsonFichas: Fixtures.Leer("ConsultarAdvertenciasProgramacion", "fichas-grupo.json"));

        await Ejecutar(fakes.Tool, sede: "NORTE", codigos: "C-103, C-999");

        Codigos(BodyDe(fakes.Turnos)).Should().Equal("C-103");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeFraseSinLlamarATurnos_CuandoElGrupoNoTieneColaboradores()
    {
        var fakes = Armar();

        var resultado = await Ejecutar(fakes.Tool, sede: "NORTE");

        resultado.Should().Contain("NORTE").And.Contain("2026-07-01").And.Contain("2026-07-31");
        resultado.Should().NotStartWith("{");
        fakes.Turnos.UltimaRequest.Should().BeNull("una lista vacia significaria sin filtro (#1045)");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeFraseSinLlamarATurnos_CuandoLaInterseccionEsVacia()
    {
        var fakes = Armar(jsonFichas: Fixtures.Leer("ConsultarAdvertenciasProgramacion", "fichas-grupo.json"));

        var resultado = await Ejecutar(fakes.Tool, sede: "NORTE", codigos: "C-999");

        resultado.Should().Contain("NORTE").And.Contain("C-999");
        resultado.Should().NotStartWith("{");
        fakes.Turnos.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeElRechazoSinSeguir_CuandoLaLecturaDeFichasFalla()
    {
        var fakes = Armar(jsonFichas: "fichas no disponibles", statusFichas: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(fakes.Tool, sede: "NORTE");

        resultado.Should().StartWith("El dominio rechazo la consulta:").And.Contain("fichas no disponibles");
        fakes.Turnos.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_EnviaElSedeIdResuelto_CuandoSePasaSedeTrabajo()
    {
        var fakes = Armar();

        await Ejecutar(fakes.Tool, sedeTrabajo: "NORTE");

        fakes.Sedes.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/sedes/fichas/NORTE");
        BodyDe(fakes.Turnos)["sedeId"]!.GetValue<string>().Should().Be("NORTE");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeFraseSinLlamarATurnos_CuandoLaSedeTrabajoNoExiste()
    {
        var fakes = Armar(jsonSede: "", statusSede: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, sedeTrabajo: "FANTASMA");

        resultado.Should().Be(string.Format(ConsultarProgramacionTool.Mensajes.SedeTrabajoNoExiste, "FANTASMA"));
        fakes.Turnos.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeElRechazoSinSeguir_CuandoLaLecturaDeLaSedeTrabajoFalla()
    {
        var fakes = Armar(jsonSede: "sedes no disponibles", statusSede: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, sedeTrabajo: "NORTE");

        resultado.Should().StartWith("El dominio rechazo la consulta:").And.Contain("sedes no disponibles");
        fakes.Turnos.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_ConsultaTurnosConElSedeId_CuandoLaSedeTrabajoEstaInactiva()
    {
        var fakes = Armar(jsonSede: Leer("ficha-sede-inactiva.json"));

        var resultado = await Ejecutar(fakes.Tool, sedeTrabajo: "NORTE");

        BodyDe(fakes.Turnos)["sedeId"]!.GetValue<string>().Should().Be("NORTE");
        resultado.Should().StartWith("{");
    }

    [Fact]
    public async Task ConsultarProgramacion_CombinaSujetosYSedeTrabajoEnAnd_CuandoVienenAmbos()
    {
        var fakes = Armar(jsonFichas: Fixtures.Leer("ConsultarAdvertenciasProgramacion", "fichas-grupo.json"));

        await Ejecutar(fakes.Tool, sede: "NORTE", sedeTrabajo: "NORTE");

        var body = BodyDe(fakes.Turnos);
        Codigos(body).Should().Equal("C-102", "C-103");
        body["sedeId"]!.GetValue<string>().Should().Be("NORTE");
    }

    [Fact]
    public async Task ConsultarProgramacion_RemodelaLosDias_CuandoHayProgramacion()
    {
        var fakes = Armar();

        var resultado = await Ejecutar(fakes.Tool);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-07-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-07-31");
        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.ContainsKey("total").Should().BeFalse("la ventana temporal no reporta total (CA-ADR-0039)");
        json.ContainsKey("turnos").Should().BeFalse("la lista se llama dias");
        json.ContainsKey("nota").Should().BeFalse("sin recorte ni mas paginas no hay senal");
        json.ContainsKey("siguienteCursor").Should().BeFalse();

        var primero = json["dias"]![0]!.AsObject();
        primero["colaborador"]!.GetValue<string>().Should().Be("01a04dd1-0db3-7178-92ec-d6a7fd362255");
        primero["nombre"]!.GetValue<string>().Should().Be("[TEST] Smoke Persistencia CC");
        primero["fecha"]!.GetValue<string>().Should().Be("2026-07-03");
        primero["turno"]!.GetValue<string>().Should().Be("[TEST] Turno Smoke CC Persistencia");
        primero["bloques"]!.AsArray().Select(b => b!.GetValue<string>()).Should()
            .Equal("08:00-16:00, sede: [TEST] Sede Persistencia CC");
        primero.ContainsKey("id").Should().BeFalse("el stream key 'cd:...' es interno");
        primero.ContainsKey("horarioResumido").Should().BeFalse("los bloques compactos son la forma canonica");

        json["dias"]![2]!.AsObject().ContainsKey("nombre").Should().BeFalse("nombreCompleto null se omite");
    }

    [Fact]
    public async Task ConsultarProgramacion_CompactaDescansoExtraYCruceDeMedianoche_CuandoLosBloquesLosTraen()
    {
        var fakes = Armar(Leer("turnos-vigentes-con-descanso-variante.json"));

        var resultado = await Ejecutar(fakes.Tool);

        JsonNode.Parse(resultado)!["dias"]![0]!["bloques"]!.AsArray().Select(b => b!.GetValue<string>())
            .Should().Equal("08:00-12:00", "descanso 12:00-13:00", "extra 22:00-02:00(+1), sede: Sede Nocturna");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeTodosLosDiasRecibidosConCursorYNota_CuandoElBackendTrajoSiguienteCursor()
    {
        var fakes = Armar(ConCursor(Leer("turnos-vigentes-rango-grande.json"), "cursor-siguiente"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-06-01", hasta: "2026-06-30");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["dias"]!.AsArray().Should().HaveCount(60, "la pagina la acota el backend con take, no la tool");
        json["mostrando"]!.GetValue<int>().Should().Be(60);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("cursor-siguiente");
        json["nota"]!.GetValue<string>().Should().Contain(ConsultarProgramacionTool.Mensajes.NotaMasDias);
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarProgramacion_NoTraeCursorNiNotaDeMas_CuandoElBackendNoTrajoSiguienteCursor()
    {
        var fakes = Armar(Leer("turnos-vigentes-rango-grande.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-06-01", hasta: "2026-06-30");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ConsultarProgramacion_SenalaElRecorteYTraeElRangoAplicado_CuandoElDominioRecortoElRango()
    {
        var fakes = Armar(Leer("turnos-vigentes-recortado-variante.json"));

        var resultado = await Ejecutar(fakes.Tool, hasta: "2027-12-31");

        var json = JsonNode.Parse(resultado)!;
        json["nota"]!.GetValue<string>().Should().Contain(ConsultarProgramacionTool.Mensajes.NotaRecorte);
        json["hasta"]!.GetValue<string>().Should().Be("2026-07-31", "hasta refleja el rango aplicado");
    }

    [Fact]
    public async Task ConsultarProgramacion_UneRecorteYHayMas_CuandoAmbosAplican()
    {
        var fakes = Armar(ConCursor(Leer("turnos-vigentes-recortado-variante.json"), "otro"));

        var resultado = await Ejecutar(fakes.Tool, hasta: "2027-12-31");

        var nota = JsonNode.Parse(resultado)!["nota"]!.GetValue<string>();
        nota.Should().Contain(ConsultarProgramacionTool.Mensajes.NotaRecorte)
            .And.Contain(ConsultarProgramacionTool.Mensajes.NotaMasDias);
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeFraseConPeriodoYCriterio_CuandoNoHayDiasProgramados()
    {
        var fakes = Armar(Leer("turnos-vigentes-vacio.json"), jsonSede: Leer("ficha-sede.json"));

        var resultado = await Ejecutar(fakes.Tool, codigos: "COL-1", sedeTrabajo: "NORTE");

        resultado.Should().NotStartWith("{");
        resultado.Should().Contain("2026-07-01").And.Contain("2026-07-31")
            .And.Contain("COL-1").And.Contain("NORTE");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeMensajeSinLlamarAlDominio_CuandoUnaFechaEsInvalida()
    {
        var fakes = Armar("{}");

        var resultado = await Ejecutar(fakes.Tool, desde: "01/07/2026");

        resultado.Should().Contain("yyyy-MM-dd").And.Contain("01/07/2026");
        fakes.Turnos.UltimaRequest.Should().BeNull("la validacion es previa a la llamada HTTP");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeMensajeSinLlamarAlDominio_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = Armar("{}");

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-07-31", hasta: "2026-07-01");

        resultado.Should().Be(ConsultarProgramacionTool.Mensajes.DesdePosteriorAHasta);
        fakes.Turnos.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_TraduceElRechazo_CuandoElDominioDevuelve422()
    {
        var fakes = Armar("DesdeFecha y HastaFecha son obligatorios", statusTurnos: HttpStatusCode.UnprocessableEntity);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().StartWith("El dominio rechazo la consulta:")
            .And.Contain("DesdeFecha y HastaFecha son obligatorios");
    }
}
