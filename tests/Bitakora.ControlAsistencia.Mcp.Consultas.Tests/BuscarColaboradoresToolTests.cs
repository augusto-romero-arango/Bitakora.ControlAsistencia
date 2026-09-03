using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Consultas.BuscarColaboradores;
using Bitakora.ControlAsistencia.Mcp.Consultas.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Consultas.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Consultas.Tests;

public class BuscarColaboradoresToolTests
{
    private static BuscarColaboradoresTool Tool(HttpClient cliente) => new(new ColaboradoresApi(cliente));

    [Fact]
    public async Task BuscarColaboradores_EnviaElQueryConNombre_CuandoSoloNombreEstaPresente()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        await Tool(cliente).Run(null!, "juan bermúdez", null, TestContext.Current.CancellationToken);

        handler.UltimaRequest!.Method.Method.Should().Be("QUERY");
        handler.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/colaboradores/directorio");

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["nombre"]!.GetValue<string>().Should().Be("juan bermúdez");
        body["take"]!.GetValue<int>().Should().Be(BuscarColaboradoresTool.TakeUpstream);
        body.ContainsKey("identificaciones").Should().BeFalse("sin identificaciones no viaja el campo");
    }

    [Fact]
    public async Task BuscarColaboradores_EnviaElQueryConIdentificacionUnica_CuandoIdentificacionesTraeUnValor()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        await Tool(cliente).Run(null!, null, "79879078", TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body.ContainsKey("nombre").Should().BeFalse("sin nombre no viaja el campo");
        var identificaciones = body["identificaciones"]!.AsArray();
        identificaciones.Select(v => v!.GetValue<string>()).Should().Equal("79879078");
    }

    [Fact]
    public async Task BuscarColaboradores_EnviaElQueryConIdentificacionesNormalizadas_CuandoTraenEspaciosYVacios()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        await Tool(cliente).Run(
            null!, null, " CC-79879078 , 10047766882,, ", TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        var identificaciones = body["identificaciones"]!.AsArray();
        identificaciones.Select(v => v!.GetValue<string>()).Should()
            .Equal("CC-79879078", "10047766882");
    }

    [Fact]
    public async Task BuscarColaboradores_EnviaAmbosCriterios_CuandoNombreEIdentificacionesEstanPresentes()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        await Tool(cliente).Run(
            null!, "juan", "79879078,10047766882", TestContext.Current.CancellationToken);

        var body = JsonNode.Parse(handler.UltimoCuerpoEnviado!)!.AsObject();
        body["nombre"]!.GetValue<string>().Should().Be("juan");
        body["identificaciones"]!.AsArray().Select(v => v!.GetValue<string>()).Should()
            .Equal("79879078", "10047766882");
    }

    [Fact]
    public async Task BuscarColaboradores_RemodelaLaListaTokenEficiente_CuandoHayCoincidencias()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        var resultado = await Tool(cliente).Run(
            null!, "juan", null, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["total"]!.GetValue<int>().Should().Be(3);
        json["mostrando"]!.GetValue<int>().Should().Be(3);
        json.ContainsKey("nota").Should().BeFalse("sin truncado no hay senal");

        var primero = json["colaboradores"]![0]!.AsObject();
        primero["identificacion"]!.GetValue<string>().Should().Be("CC-79879078");
        primero["nombre"]!.GetValue<string>().Should().Be("[TEST] Juan Pablo Bermudez");
        primero["codigoColaborador"]!.GetValue<string>().Should().Be("COL-10");
        primero["codigoSede"]!.GetValue<string>().Should().Be("SEDE-NORTE");
        primero["vigenteDesde"]!.GetValue<string>().Should().Be("2024-01-15");
        primero.ContainsKey("vigenteHasta").Should().BeFalse("vinculacion abierta = campo ausente");
        primero.ContainsKey("tipoDocumento").Should().BeFalse("redundante con la identificacion completa");
        primero.ContainsKey("numeroDocumento").Should().BeFalse("redundante con la identificacion completa");
        primero.ContainsKey("tokensNombre").Should().BeFalse("estructura interna del dominio, no viaja");

        var segundo = json["colaboradores"]![1]!.AsObject();
        segundo["vigenteHasta"]!.GetValue<string>().Should().Be("2026-06-30");

        var tercero = json["colaboradores"]![2]!.AsObject();
        tercero.ContainsKey("codigoSede").Should().BeFalse("sin sede asignada, null se omite");
    }

    [Fact]
    public async Task BuscarColaboradores_TruncaConSenal_CuandoLaRespuestaTraeMasDe20Coincidencias()
    {
        var (cliente, _) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores-grande.json"));

        var resultado = await Tool(cliente).Run(
            null!, "juan", null, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!;
        json["total"]!.GetValue<int>().Should().Be(25);
        json["mostrando"]!.GetValue<int>().Should().Be(BuscarColaboradoresTool.MaximoColaboradores);
        json["colaboradores"]!.AsArray().Should().HaveCount(BuscarColaboradoresTool.MaximoColaboradores);
        json["nota"]!.GetValue<string>().Should().Contain("20 de 25");
    }

    [Fact]
    public async Task BuscarColaboradores_RespondeFaltaCriterioSinLlamarAlDominio_CuandoNombreEIdentificacionesEstanAusentes()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        var resultado = await Tool(cliente).Run(null!, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(
            "Indica un nombre (palabras completas) o una o varias identificaciones para buscar.");
        handler.UltimaRequest.Should().BeNull("sin criterio no debe llamar al endpoint");
    }

    [Fact]
    public async Task BuscarColaboradores_RespondeFaltaCriterioSinLlamarAlDominio_CuandoSoloHayEspaciosYComas()
    {
        var (cliente, handler) = ClienteFalso.Con(Fixtures.Leer("buscar-colaboradores.json"));

        var resultado = await Tool(cliente).Run(
            null!, "   ", " , , ,", TestContext.Current.CancellationToken);

        resultado.Should().Be(
            "Indica un nombre (palabras completas) o una o varias identificaciones para buscar.");
        handler.UltimaRequest.Should().BeNull("espacios y comas vacias no son un criterio valido");
    }

    [Fact]
    public async Task BuscarColaboradores_RespondeElRechazoDelDominio_CuandoElDominioResponde422()
    {
        var (cliente, _) = ClienteFalso.Con(
            "Maximo 200 identificaciones por consulta", HttpStatusCode.UnprocessableEntity);

        var resultado = await Tool(cliente).Run(
            null!, null, string.Join(',', Enumerable.Range(1, 201)), TestContext.Current.CancellationToken);

        resultado.Should().StartWith("El dominio rechazo la busqueda:")
            .And.Contain("Maximo 200 identificaciones por consulta");
    }
}
