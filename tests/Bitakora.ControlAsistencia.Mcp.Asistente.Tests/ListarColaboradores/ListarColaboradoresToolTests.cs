using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarColaboradores;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarColaboradores;

public class ListarColaboradoresToolTests
{
    // 2026-08-30T15:00:00Z = 2026-08-30T10:00:00 en America/Bogota (UTC-5): "hoy" no es ambiguo.
    private static readonly DateTimeOffset AhoraFalso = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);

    private const string JornadaAsignada = "11111111-1111-1111-1111-111111111111";
    private const string JornadaPredeterminada = "22222222-2222-2222-2222-222222222222";
    private const string JornadaDesactivada = "33333333-3333-3333-3333-333333333333";
    private const string JornadaInexistente = "44444444-4444-4444-4444-444444444444";

    private const string CatalogoDeJornadas = """
        {"elementos":[
          {"jornadaId":"11111111-1111-1111-1111-111111111111","descripcion":"42 h semanales, tope diario 8 h","esPredeterminada":false,"activa":true},
          {"jornadaId":"22222222-2222-2222-2222-222222222222","descripcion":"44 h semanales, tope diario 9 h","esPredeterminada":true,"activa":true},
          {"jornadaId":"33333333-3333-3333-3333-333333333333","descripcion":"36 h semanales, tope diario 6 h","esPredeterminada":false,"activa":false}
        ]}
        """;

    private static ListarColaboradoresTool Tool(HttpClient cliente, DateTimeOffset? ahora = null) =>
        Tool(cliente, ClienteFalso.ConFuncion(_ => ClienteFalso.JsonOk(CatalogoDeJornadas)).Cliente, ahora);

    private static ListarColaboradoresTool Tool(HttpClient fichas, HttpClient jornadas, DateTimeOffset? ahora = null) =>
        new(new ColaboradoresApi(fichas), new ProgramacionApi(jornadas), new RelojFalso(ahora ?? AhoraFalso));

    private static string Ficha(string id, string? jornadaId) =>
        $$"""
        {"id":"{{id}}","nombreCompleto":"[TEST] {{id}}","codigoColaborador":"COL-{{id}}","vigenteDesde":"2024-01-15",
         "vigenteHasta":null,"etiquetas":[],"codigoSede":null,"jornadaId":{{(jornadaId is null ? "null" : $"\"{jornadaId}\"")}}}
        """;

    private static string Sobre(params string[] fichas) => $$"""{"elementos":[{{string.Join(",", fichas)}}]}""";

    [Fact]
    public async Task ListarColaboradores_EnviaElQueryConFechaReferenciaResuelta_CuandoFechaReferenciaEstaAusente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/colaboradores/fichas");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-08-30", "el back jamas resuelve 'hoy' (decision #373)");
    }

    [Fact]
    public async Task ListarColaboradores_EnviaElQueryConLosFiltrosCombinadosEnAnd_CuandoSedeYEtiquetasEstanPresentes()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, "SEDE-9", "area:tecnologia,turno:diurno", "2026-07-01", null, TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-07-01");
        body["codigoSede"]!.GetValue<string>().Should().Be("SEDE-9");
        body["take"]!.GetValue<int>().Should().Be(50);

        var etiquetas = body["etiquetas"]!.AsArray();
        etiquetas.Should().HaveCount(2);
        etiquetas[0]!["categoria"]!.GetValue<string>().Should().Be("area");
        etiquetas[0]!["valor"]!.GetValue<string>().Should().Be("tecnologia");
        etiquetas[1]!["categoria"]!.GetValue<string>().Should().Be("turno");
        etiquetas[1]!["valor"]!.GetValue<string>().Should().Be("diurno");
    }

    [Fact]
    public async Task ListarColaboradores_RemodelaLaListaTokenEficiente_CuandoHayColaboradores()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json.ContainsKey("total").Should().BeFalse("CA-ADR-0039: sobre sin total");
        json["mostrando"]!.GetValue<int>().Should().Be(3);
        json.ContainsKey("nota").Should().BeFalse("sin cursor no hay senal");
        json.ContainsKey("siguienteCursor").Should().BeFalse();

        var primero = json["colaboradores"]![0]!.AsObject();
        primero["identificacion"]!.GetValue<string>().Should().Be("CC-1098765432");
        primero["nombre"]!.GetValue<string>().Should().Be("[TEST] Luis Augusto Barreto");
        primero["codigoSede"]!.GetValue<string>().Should().Be("SEDE-NORTE", "sin nombre resuelto (MEF-ADR-0018)");
        primero["vigenteDesde"]!.GetValue<string>().Should().Be("2024-01-15");
        primero.ContainsKey("vigenteHasta").Should().BeFalse("vinculacion abierta = campo ausente, sin centinela");
        primero["codigoColaborador"]!.GetValue<string>().Should()
            .Be("COL-1", "es la llave con que consultar_programacion filtra");
        primero["etiquetas"]!.AsArray().Select(e => e!.GetValue<string>()).Should()
            .Equal("Area:Tecnologia", "Turno:Diurno");
        primero.ContainsKey("etiquetasNormalizadas").Should().BeFalse("estructura interna de filtrado, no viaja");

        var segundo = json["colaboradores"]![1]!.AsObject();
        segundo["vigenteHasta"]!.GetValue<string>().Should().Be("2026-12-31");
        segundo.ContainsKey("codigoSede").Should().BeFalse("sin sede asignada, null se omite");
        segundo.ContainsKey("etiquetas").Should().BeFalse("sin etiquetas, lista vacia se omite");
    }

    [Fact]
    public async Task ListarColaboradores_PideTakeIgualAlTope_CuandoListaElCatalogo()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["take"]!.GetValue<int>().Should().Be(50, "tope fijo del catalogo, no tope + 1 (#1057)");
        body.ContainsKey("cursor").Should().BeFalse("sin cursor recibido se pide la primera pagina");
        ListarColaboradoresTool.MaximoColaboradores.Should().Be(50);
    }

    [Fact]
    public async Task ListarColaboradores_ReenviaElCursorYLaNota_CuandoElBackendTraeSiguienteCursor()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores-con-cursor.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["mostrando"]!.GetValue<int>().Should().Be(3);
        json["colaboradores"]!.AsArray().Should().HaveCount(3);
        json["siguienteCursor"]!.GetValue<string>().Should().Be("eyJJZCI6IkNDLTEwOTg3NjU0MzIifQ");
        json["nota"]!.GetValue<string>().Should().Be(ListarColaboradoresTool.Mensajes.NotaHayMasColaboradores);
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ListarColaboradores_EnviaElCursorTalCualEnElBody_CuandoRecibeCursor()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, "SEDE-9", "area:tecnologia", "2026-07-01", "abc_-DEF123",
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["cursor"]!.GetValue<string>().Should().Be("abc_-DEF123");
        body["codigoSede"]!.GetValue<string>().Should().Be("SEDE-9");
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-07-01");
        body["etiquetas"]!.AsArray().Should().HaveCount(1);
    }

    [Fact]
    public async Task ListarColaboradores_IgnoraElCursor_CuandoIdentificacionEstaPresente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "obtener-colaborador.json"));

        var resultado = await Tool(cliente).Run(null!, "CC-1098765432", null, null, null, "abc_-DEF123",
            TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Get);
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/colaboradores/fichas/CC-1098765432");
        handler.UltimaRequest.RequestUri.Query.Should().NotContain("cursor");
        JsonNode.Parse(resultado)!["identificacion"]!.GetValue<string>().Should().Be("CC-1098765432");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeFraseConElCriterio_CuandoNoHayColaboradores()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores-vacio.json"));

        var resultado = await Tool(cliente).Run(null!, null, "SEDE-9", "area:tecnologia", null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(
            "No hay colaboradores vinculados con fecha de referencia 2026-08-30, sede SEDE-9, etiquetas area:tecnologia.",
            "es una frase con el criterio usado, nunca un sobre vacio");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeFraseConLaFecha_CuandoNoHayColaboradoresSinFiltros()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores-vacio.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, null, "2026-07-01", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be("No hay colaboradores vinculados con fecha de referencia 2026-07-01.");
    }

    [Fact]
    public async Task ListarColaboradores_ConsultaLaFichaPuntual_CuandoIdentificacionEstaPresente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "obtener-colaborador.json"));

        var resultado = await Tool(cliente).Run(null!, "CC-1098765432", "SEDE-9", "area:tecnologia", "2026-07-01", null, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Should().Be(HttpMethod.Get, "los demas filtros no aplican en la ruta puntual");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should()
            .Be("/api/colaboradores/fichas/CC-1098765432");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["identificacion"]!.GetValue<string>().Should().Be("CC-1098765432");
        json["nombre"]!.GetValue<string>().Should().Be("[TEST] Luis Augusto Barreto");
        json["codigoSede"]!.GetValue<string>().Should().Be("SEDE-NORTE");
        json.ContainsKey("total").Should().BeFalse("la ficha puntual no lleva envelope de listado");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeMensajeClaro_CuandoElColaboradorNoExiste()
    {
        var (cliente, _) = ClienteFalso.Con("", HttpStatusCode.NotFound);

        var resultado = await Tool(cliente).Run(null!, "CC-999999", null, null, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be("No existe un colaborador con identificacion 'CC-999999'.");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeElRechazoDelDominio_CuandoLaIdentificacionTieneFormatoInvalido()
    {
        var (cliente, _) = ClienteFalso.Con(
            "El id de la ruta es invalido -- debe tener la forma {Tipo}-{Numero}",
            HttpStatusCode.BadRequest);

        var resultado = await Tool(cliente).Run(null!, "1098765432", null, null, null, null, TestContext.Current.CancellationToken);

        resultado.Should().StartWith("El dominio rechazo la consulta:")
            .And.Contain("{Tipo}-{Numero}", "el asistente necesita saber que forma corregir");
    }

    [Fact]
    public async Task ListarColaboradores_OmiteLosFiltrosEnBlanco_CuandoElAsistenteEnviaCadenasVacias()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ListarColaboradores", "listar-colaboradores.json"));

        await Tool(cliente).Run(null!, "  ", "  ", "area:,  ,sin-separador", null, null, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY", "identificacion en blanco no es una ficha puntual");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["codigoSede"].Should().BeNull("una sede en blanco es 422 upstream, no un filtro");
        body["etiquetas"].Should().BeNull("ningun par quedo completo tras el parseo");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeMensajeSinLlamarAlDominio_CuandoFechaReferenciaEsInvalida()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, null, null, null, "31/07/2026", null, TestContext.Current.CancellationToken);

        resultado.Should().Contain("yyyy-MM-dd").And.Contain("31/07/2026");
        handler.UltimaRequest.Should().BeNull("la validacion de formato es previa a la llamada HTTP");
    }

    [Fact]
    public async Task ListarColaboradores_TraeLaJornadaAsignadaOLaPredeterminada_CuandoHayColaboradoresConYSinJornada()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", JornadaAsignada), Ficha("CC-2", null))).Cliente;
        var (jornadas, handler) = ClienteFalso.ConFuncion(_ => ClienteFalso.JsonOk(CatalogoDeJornadas));

        var resultado = await Tool(fichas, jornadas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var colaboradores = JsonNode.Parse(resultado)!["colaboradores"]!.AsArray();
        var asignada = colaboradores[0]!["jornada"]!.AsObject();
        asignada["jornada_id"]!.GetValue<string>().Should().Be(JornadaAsignada);
        asignada["descripcion"]!.GetValue<string>().Should().Be("42 h semanales, tope diario 8 h");
        asignada.ContainsKey("por_defecto").Should().BeFalse("tiene jornada asignada");
        asignada.ContainsKey("activa").Should().BeFalse("activa se omite cuando es true");

        var porDefecto = colaboradores[1]!["jornada"]!.AsObject();
        porDefecto["jornada_id"]!.GetValue<string>().Should().Be(JornadaPredeterminada);
        porDefecto["descripcion"]!.GetValue<string>().Should().Be("44 h semanales, tope diario 9 h");
        porDefecto["por_defecto"]!.GetValue<bool>().Should().BeTrue();

        handler.Requests.Should().ContainSingle("una sola llamada al catalogo, sin importar cuantos colaboradores");
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/api/programacion/jornadas");
        handler.Requests[0].RequestUri!.Query.Should().BeEmpty("composicion interna sin take (CA-ADR-0038)");
    }

    [Fact]
    public async Task ListarColaboradores_HaceUnaSolaLlamadaAlCatalogo_CuandoHayVariosColaboradores()
    {
        var fichas = ClienteFalso.Con(Sobre(
            Ficha("CC-1", JornadaAsignada), Ficha("CC-2", null), Ficha("CC-3", JornadaDesactivada),
            Ficha("CC-4", JornadaAsignada))).Cliente;
        var (jornadas, handler) = ClienteFalso.ConFuncion(_ => ClienteFalso.JsonOk(CatalogoDeJornadas));

        await Tool(fichas, jornadas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task ListarColaboradores_NoMarcaPorDefecto_CuandoTieneAsignadaLaQueHoyEsPredeterminada()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", JornadaPredeterminada))).Cliente;

        var resultado = await Tool(fichas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["colaboradores"]![0]!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaPredeterminada);
        jornada["descripcion"]!.GetValue<string>().Should().Be("44 h semanales, tope diario 9 h");
        jornada.ContainsKey("por_defecto").Should().BeFalse("por_defecto dice que no tiene ninguna asignada");
    }

    [Fact]
    public async Task ListarColaboradores_MarcaActivaFalse_CuandoLaJornadaEstaDesactivada()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", JornadaDesactivada))).Cliente;

        var resultado = await Tool(fichas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["colaboradores"]![0]!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaDesactivada);
        jornada["descripcion"]!.GetValue<string>().Should().Be("36 h semanales, tope diario 6 h");
        jornada["activa"]!.GetValue<bool>().Should().BeFalse();
    }

    [Fact]
    public async Task ListarColaboradores_TraeSoloElIdDeJornada_CuandoLaJornadaNoEstaEnElCatalogo()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", JornadaInexistente))).Cliente;

        var resultado = await Tool(fichas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["colaboradores"]![0]!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaInexistente);
        jornada.ContainsKey("descripcion").Should().BeFalse();
        jornada.ContainsKey("por_defecto").Should().BeFalse();
        jornada.ContainsKey("activa").Should().BeFalse();
    }

    [Fact]
    public async Task ListarColaboradores_OmiteLaJornada_CuandoNoTieneAsignadaYElCatalogoNoTraePredeterminada()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", null))).Cliente;
        var jornadas = ClienteFalso.Con("""{"elementos":[]}""").Cliente;

        var resultado = await Tool(fichas, jornadas).Run(null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["colaboradores"]![0]!.AsObject().ContainsKey("jornada").Should()
            .BeFalse("sin asignada ni predeterminada no hay jornada que mostrar, nunca un jornada_id vacio");
    }

    [Fact]
    public async Task ListarColaboradores_NoLlamaAlCatalogoDeJornadas_CuandoNoHayColaboradores()
    {
        var fichas = ClienteFalso.Con(Sobre()).Cliente;
        var (jornadas, handler) = ClienteFalso.ConFuncion(_ => ClienteFalso.JsonOk(CatalogoDeJornadas));

        var resultado = await Tool(fichas, jornadas).Run(null!, null, null, null, "2026-07-01", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be("No hay colaboradores vinculados con fecha de referencia 2026-07-01.");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListarColaboradores_LanzaExcepcion_CuandoElCatalogoDeJornadasFalla()
    {
        var fichas = ClienteFalso.Con(Sobre(Ficha("CC-1", JornadaAsignada))).Cliente;
        var jornadas = ClienteFalso.Con("", HttpStatusCode.InternalServerError).Cliente;

        var act = async () => await Tool(fichas, jornadas).Run(
            null!, null, null, null, null, null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ListarColaboradores_TraeLaJornadaAsignadaEnLaFichaPuntual_CuandoIdentificacionEstaPresente()
    {
        var fichas = ClienteFalso.Con(Ficha("CC-1", JornadaAsignada)).Cliente;

        var resultado = await Tool(fichas).Run(null!, "CC-1", null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaAsignada);
        jornada["descripcion"]!.GetValue<string>().Should().Be("42 h semanales, tope diario 8 h");
        jornada.ContainsKey("por_defecto").Should().BeFalse();
    }

    [Fact]
    public async Task ListarColaboradores_TraeLaPredeterminadaEnLaFichaPuntual_CuandoNoTieneJornadaAsignada()
    {
        var fichas = ClienteFalso.Con(Ficha("CC-1", null)).Cliente;

        var resultado = await Tool(fichas).Run(null!, "CC-1", null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaPredeterminada);
        jornada["por_defecto"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task ListarColaboradores_MarcaActivaFalseEnLaFichaPuntual_CuandoLaJornadaEstaDesactivada()
    {
        var fichas = ClienteFalso.Con(Ficha("CC-1", JornadaDesactivada)).Cliente;

        var resultado = await Tool(fichas).Run(null!, "CC-1", null, null, null, null, TestContext.Current.CancellationToken);

        var jornada = JsonNode.Parse(resultado)!["jornada"]!.AsObject();
        jornada["jornada_id"]!.GetValue<string>().Should().Be(JornadaDesactivada);
        jornada["activa"]!.GetValue<bool>().Should().BeFalse();
    }
}
