using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.ListarColaboradores;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ListarColaboradoresToolTests
{
    // 2026-08-30T15:00:00Z = 2026-08-30T10:00:00 en America/Bogota (UTC-5): "hoy" no es ambiguo.
    private static readonly DateTimeOffset AhoraFalso = new(2026, 8, 30, 15, 0, 0, TimeSpan.Zero);

    private static ListarColaboradoresTool Tool(HttpClient cliente, DateTimeOffset? ahora = null) =>
        new(new ColaboradoresApi(cliente), new RelojFalso(ahora ?? AhoraFalso));

    [Fact]
    public async Task ListarColaboradores_EnviaElQueryConFechaReferenciaResuelta_CuandoFechaReferenciaEstaAusente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, null, null, null, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/colaboradores/fichas");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-08-30", "el back jamas resuelve 'hoy' (decision #373)");
    }

    [Fact]
    public async Task ListarColaboradores_EnviaElQueryConLosFiltrosCombinadosEnAnd_CuandoSedeYEtiquetasEstanPresentes()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("listar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, "SEDE-9", "area:tecnologia,turno:diurno", "2026-07-01",
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["fechaReferencia"]!.GetValue<string>().Should().Be("2026-07-01");
        body["codigoSede"]!.GetValue<string>().Should().Be("SEDE-9");
        body["take"]!.GetValue<int>().Should().Be(ListarColaboradoresTool.TakeUpstream);

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
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("listar-colaboradores.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["total"]!.GetValue<int>().Should().Be(3);
        json["mostrando"]!.GetValue<int>().Should().Be(3);
        json.ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");

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
    public async Task ListarColaboradores_TruncaConSenal_CuandoLaRespuestaTraeMasFilasQueElMaximo()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("listar-colaboradores-grande.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["mostrando"]!.GetValue<int>().Should().Be(ListarColaboradoresTool.MaximoColaboradores);
        json["colaboradores"]!.AsArray().Should().HaveCount(ListarColaboradoresTool.MaximoColaboradores);
        json["nota"]!.GetValue<string>().Should().Contain("20 de 25");
    }

    [Fact]
    public async Task ListarColaboradores_ConsultaLaFichaPuntual_CuandoIdentificacionEstaPresente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("obtener-colaborador.json"));

        var resultado = await Tool(cliente).Run(
            null!, "CC-1098765432", "SEDE-9", "area:tecnologia", "2026-07-01",
            TestContext.Current.CancellationToken);

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

        var resultado = await Tool(cliente).Run(
            null!, "CC-999999", null, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be("No existe un colaborador con identificacion 'CC-999999'.");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeElRechazoDelDominio_CuandoLaIdentificacionTieneFormatoInvalido()
    {
        var (cliente, _) = ClienteFalso.Con(
            "El id de la ruta es invalido -- debe tener la forma {Tipo}-{Numero}",
            HttpStatusCode.BadRequest);

        var resultado = await Tool(cliente).Run(
            null!, "1098765432", null, null, null, TestContext.Current.CancellationToken);

        resultado.Should().StartWith("El dominio rechazo la consulta:")
            .And.Contain("{Tipo}-{Numero}", "el asistente necesita saber que forma corregir");
    }

    [Fact]
    public async Task ListarColaboradores_OmiteLosFiltrosEnBlanco_CuandoElAsistenteEnviaCadenasVacias()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("listar-colaboradores.json"));

        await Tool(cliente).Run(null!, "  ", "  ", "area:,  ,sin-separador", null,
            TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY", "identificacion en blanco no es una ficha puntual");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["codigoSede"].Should().BeNull("una sede en blanco es 422 upstream, no un filtro");
        body["etiquetas"].Should().BeNull("ningun par quedo completo tras el parseo");
    }

    [Fact]
    public async Task ListarColaboradores_RespondeMensajeSinLlamarAlDominio_CuandoFechaReferenciaEsInvalida()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(
            null!, null, null, null, "31/07/2026", TestContext.Current.CancellationToken);

        resultado.Should().Contain("yyyy-MM-dd").And.Contain("31/07/2026");
        handler.UltimaRequest.Should().BeNull("la validacion de formato es previa a la llamada HTTP");
    }
}
