using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.ConsultarProgramacion;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class ConsultarProgramacionToolTests
{
    private static ConsultarProgramacionTool Tool(HttpClient cliente) =>
        new(new ControlHorasApi(cliente));

    [Fact]
    public async Task ConsultarProgramacion_EnviaElQueryConElFiltro_CuandoLasFechasSonValidas()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("turnos-vigentes.json"));

        await Tool(cliente).Run(null!, "2026-07-01", "2026-07-31", "COL-1", "SEDE-9",
            TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/control-horas/turnos-vigentes");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["desdeFecha"]!.GetValue<string>().Should().Be("2026-07-01");
        body["hastaFecha"]!.GetValue<string>().Should().Be("2026-07-31");
        body["codigoColaborador"]!.GetValue<string>().Should().Be("COL-1");
        body["sedeId"]!.GetValue<string>().Should().Be("SEDE-9");
    }

    [Fact]
    public async Task ConsultarProgramacion_RemodelaLosDias_CuandoHayProgramacion()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("turnos-vigentes.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-07-01", "2026-07-31", null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["desde"]!.GetValue<string>().Should().Be("2026-07-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-07-31");
        json["total"]!.GetValue<int>().Should().Be(4);
        json.ContainsKey("nota").Should().BeFalse("sin recorte ni truncado no hay senal");

        var primero = json["turnos"]![0]!.AsObject();
        primero["colaborador"]!.GetValue<string>().Should().Be("01a04dd1-0db3-7178-92ec-d6a7fd362255");
        primero["nombre"]!.GetValue<string>().Should().Be("[TEST] Smoke Persistencia CC");
        primero["fecha"]!.GetValue<string>().Should().Be("2026-07-03");
        primero["turno"]!.GetValue<string>().Should().Be("[TEST] Turno Smoke CC Persistencia");
        primero["bloques"]!.AsArray().Select(b => b!.GetValue<string>()).Should()
            .Equal("08:00-16:00, sede: [TEST] Sede Persistencia CC");
        primero.ContainsKey("id").Should().BeFalse("el stream key 'cd:...' es interno");
        primero.ContainsKey("horarioResumido").Should().BeFalse("los bloques compactos son la forma canonica");

        json["turnos"]![2]!.AsObject().ContainsKey("nombre").Should()
            .BeFalse("nombreCompleto null se omite");
    }

    [Fact]
    public async Task ConsultarProgramacion_CompactaDescansoExtraYCruceDeMedianoche_CuandoLosBloquesLosTraen()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("turnos-vigentes-con-descanso-variante.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-07-01", "2026-07-31", null, null,
            TestContext.Current.CancellationToken);

        var bloques = JsonNode.Parse(resultado)!["turnos"]![0]!["bloques"]!.AsArray()
            .Select(b => b!.GetValue<string>());

        bloques.Should().Equal(
            "08:00-12:00",
            "descanso 12:00-13:00",
            "extra 22:00-02:00(+1), sede: Sede Nocturna");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeMensajeSinLlamarAlDominio_CuandoUnaFechaEsInvalida()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "01/07/2026", "2026-07-31", null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Contain("yyyy-MM-dd").And.Contain("01/07/2026");
        handler.UltimaRequest.Should().BeNull("la validacion es previa a la llamada HTTP");
    }

    [Fact]
    public async Task ConsultarProgramacion_RespondeMensajeSinLlamarAlDominio_CuandoDesdeEsPosteriorAHasta()
    {
        var (cliente, handler) = ClienteFalso.Con("{}");

        var resultado = await Tool(cliente).Run(null!, "2026-07-31", "2026-07-01", null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().Be("'desde' no puede ser posterior a 'hasta'.");
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarProgramacion_SenalaElRecorte_CuandoElDominioRecortoElRango()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("turnos-vigentes-recortado-variante.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-07-01", "2027-12-31", null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["nota"]!.GetValue<string>().Should().Contain("recortado");
        json["hasta"]!.GetValue<string>().Should().Be("2026-07-31", "hasta refleja el rango aplicado");
    }

    [Fact]
    public async Task ConsultarProgramacion_TruncaConSenal_CuandoElRangoTraeMasDiasQueElMaximo()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("turnos-vigentes-rango-grande.json"));

        var resultado = await Tool(cliente).Run(null!, "2026-06-01", "2026-06-30", null, null,
            TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["mostrando"]!.GetValue<int>().Should().Be(ConsultarProgramacionTool.MaximoDias);
        json["turnos"]!.AsArray().Should().HaveCount(ConsultarProgramacionTool.MaximoDias);
        json["nota"]!.GetValue<string>().Should().Contain("50 de 60");
    }

    [Fact]
    public async Task ConsultarProgramacion_TraduceElRechazo_CuandoElDominioDevuelve422()
    {
        var (cliente, _) = ClienteFalso.Con(
            "DesdeFecha y HastaFecha son obligatorios", HttpStatusCode.UnprocessableEntity);

        var resultado = await Tool(cliente).Run(null!, "2026-07-01", "2026-07-31", null, null,
            TestContext.Current.CancellationToken);

        resultado.Should().StartWith("El dominio rechazo la consulta:")
            .And.Contain("DesdeFecha y HastaFecha son obligatorios");
    }
}
