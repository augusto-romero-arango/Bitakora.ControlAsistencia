using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarAusencias;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests.ConsultarAusencias;

public class ConsultarAusenciasToolTests
{
    private static ConsultarAusenciasTool Tool(HttpClient cliente) =>
        new(new ProgramacionApi(cliente));

    [Fact]
    public async Task ConsultarAusencias_EnviaElQueryConElFiltro_CuandoLasFechasSonValidas()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo.json"));

        await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", "COL-1, COL-2",
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
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo.json"));

        await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", "  ",
            TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        (body["colaboradores"] is null || body["colaboradores"]!.AsArray().Count == 0).Should().BeTrue();
    }

    [Fact]
    public async Task ConsultarAusencias_RemodelaPorColaborador_CuandoHayAusentes()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-10-12");
        json["hasta"]!.GetValue<string>().Should().Be("2026-10-18");
        json["total"]!.GetValue<int>().Should().Be(2);
        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("nota").Should().BeFalse("sin recorte ni truncado no hay senal");

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
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo-vacio.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.NadieFalta, "2026-10-12", "2026-10-18"));
    }

    [Fact]
    public async Task ConsultarAusencias_TruncaConSenal_CuandoHayMasColaboradoresQueElMaximo()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo-grande.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2026-10-07", null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["total"]!.GetValue<int>().Should().Be(60);
        json["mostrando"]!.GetValue<int>().Should().Be(ConsultarAusenciasTool.MaximoColaboradores);
        json["colaboradores"]!.AsArray().Should().HaveCount(ConsultarAusenciasTool.MaximoColaboradores);
        json["nota"]!.GetValue<string>().Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.NotaTruncado, 50, 60, 10));
    }

    [Fact]
    public async Task ConsultarAusencias_SenalaElRecorteConElPeriodoAplicado_CuandoElDominioRecortoElRango()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo-recortado.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2027-03-31", null,
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
        var respuesta = JsonNode.Parse(Fixtures.Leer("ausencias-equipo-recortado.json"))!;
        respuesta["rangoRecortado"] = false;
        var (cliente, handler) = ClienteFalso.Con(respuesta.ToJsonString());

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2026-11-04", null,
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
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("ausencias-equipo-recortado.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-10-01", "2026-11-05", null,
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

        var resultado = await Tool(cliente).Run(null!, "12/10/2026", "2026-10-18", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.FechaInvalida, "desde", "12/10/2026"));
        handler.UltimaRequest.Should().BeNull("la validacion es previa a la llamada HTTP");
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeMensajeSinLlamarAlDominio_CuandoHastaEstaAusente()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", null!, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Contain("hasta");
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_RespondeMensajeSinLlamarAlDominio_CuandoDesdeEsPosteriorAHasta()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "2026-10-18", "2026-10-12", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(ConsultarAusenciasTool.Mensajes.DesdePosteriorAHasta);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarAusencias_TraduceElRechazo_CuandoElDominioDevuelve422()
    {
        var (cliente, _) = ClienteFalso.Con(
            "Desde y Hasta son obligatorios", HttpStatusCode.UnprocessableEntity);

        var resultado = await Tool(cliente).Run(null!, "2026-10-12", "2026-10-18", null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ConsultarAusenciasTool.Mensajes.RechazoDelDominio, "Desde y Hasta son obligatorios"));
    }
}
