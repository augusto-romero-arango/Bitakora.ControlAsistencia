using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.RegistrarSede;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.RegistrarSede;

public class RegistrarSedeToolTests
{
    [Fact]
    public async Task RegistrarSede_EnviaElBodyCamelCaseYDevuelveElEcoCompacto_Cuando202()
    {
        var cliente = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted, out var handler);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "NORTE", "Sede Norte", "Bogota", "Calle 1", TestContext.Current.CancellationToken);

        handler.UltimaSolicitud!.Method.Should().Be(HttpMethod.Post);
        handler.UltimaSolicitud.RequestUri!.AbsolutePath.Should().Be("/api/sedes");

        var body = JsonNode.Parse(handler.UltimoBody!)!;
        body["codigo"]!.GetValue<string>().Should().Be("NORTE");
        body["nombre"]!.GetValue<string>().Should().Be("Sede Norte");
        body["ciudad"]!.GetValue<string>().Should().Be("Bogota");
        body["direccion"]!.GetValue<string>().Should().Be("Calle 1");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be("Sede registrada");
        json["codigo"]!.GetValue<string>().Should().Be("NORTE");
        json["nombre"]!.GetValue<string>().Should().Be("Sede Norte");
        json["ciudad"]!.GetValue<string>().Should().Be("Bogota");
        json["direccion"]!.GetValue<string>().Should().Be("Calle 1");
        json["nota"]!.GetValue<string>().Should().Be(RegistrarSedeTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task RegistrarSede_OmiteCiudadYDireccionEnElEco_CuandoNoSeEnvian()
    {
        var cliente = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "SUR", "Sede Sur", null, null, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(resultado)!.AsObject();
        json.ContainsKey("ciudad").Should().BeFalse("ciudad no llego en la llamada");
        json.ContainsKey("direccion").Should().BeFalse("direccion no llego en la llamada");
    }

    [Fact]
    public async Task RegistrarSede_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Fixtures.Leer("RegistrarSede", "validacion-400.json");
        var cliente = ClienteFalso.Con(fixture, HttpStatusCode.BadRequest);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "NORTE", "Sede Norte", null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarSedeTool.Mensajes.RechazoDelDominio, fixture));
    }

    [Fact]
    public async Task RegistrarSede_TraduceElRechazoDelDominio_Cuando409()
    {
        const string cuerpo = "La sede ya esta registrada con este codigo";
        var cliente = ClienteFalso.Con(cuerpo, HttpStatusCode.Conflict);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "NORTE", "Sede Norte", null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarSedeTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task RegistrarSede_RechazaCodigoEnBlancoSinLlamarAlDominio()
    {
        var cliente = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted, out var handler);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "   ", "Sede Norte", null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarSedeTool.Mensajes.CampoObligatorio, "codigo"));
        handler.UltimaSolicitud.Should().BeNull("un codigo en blanco no debe llegar al dominio");
    }

    [Fact]
    public async Task RegistrarSede_RechazaNombreEnBlancoSinLlamarAlDominio()
    {
        var cliente = ClienteFalso.Con(string.Empty, HttpStatusCode.Accepted, out var handler);
        var tool = new RegistrarSedeTool(new SedesApi(cliente));

        var resultado = await tool.Run(
            null!, "NORTE", "", null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(RegistrarSedeTool.Mensajes.CampoObligatorio, "nombre"));
        handler.UltimaSolicitud.Should().BeNull("un nombre en blanco no debe llegar al dominio");
    }
}
