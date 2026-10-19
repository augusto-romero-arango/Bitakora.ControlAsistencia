using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarSedes;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarSedes;

public class ListarSedesToolTests
{
    private const string Cursor = "eyJDb2RpZ28iOiJBIiwiSWQiOiIxIn0";

    private static async Task<(JsonNode Json, HandlerEnlatado Handler)> Ejecutar(string cuerpo, string? cursor = null)
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo);
        var tool = new ListarSedesTool(new SedesApi(cliente));

        var resultado = await tool.Run(null!, cursor, TestContext.Current.CancellationToken);

        return (JsonNode.Parse(resultado)!, handler);
    }

    [Fact]
    public async Task ListarSedes_PideActivasTakeIgualAlTopeSinCursor_CuandoNoRecibeCursor()
    {
        var (_, handler) = await Ejecutar(Fixtures.Leer("ListarSedes", "listar-sedes.json"));

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/sedes/fichas");
        var query = Uri.UnescapeDataString(handler.UltimaRequest.RequestUri.Query);
        query.Should().Contain("activa=true").And.Contain("take=50").And.NotContain("cursor");
        ListarSedesTool.MaximoSedes.Should().Be(50);
    }

    [Fact]
    public async Task ListarSedes_ReenviaElCursorTalCual_CuandoRecibeCursor()
    {
        var (_, handler) = await Ejecutar(Fixtures.Leer("ListarSedes", "listar-sedes.json"), Cursor);

        var query = Uri.UnescapeDataString(handler.UltimaRequest!.RequestUri!.Query);
        query.Should().Contain($"cursor={Cursor}").And.Contain("activa=true").And.Contain("take=50");
    }

    [Fact]
    public async Task ListarSedes_RemodelaYPodaLosCamposInternos_CuandoHaySedes()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarSedes", "listar-sedes.json"));

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.AsObject().ContainsKey("total").Should().BeFalse("CA-ADR-0039: sobre sin total");
        json.AsObject().ContainsKey("nota").Should().BeFalse();
        json.AsObject().ContainsKey("siguienteCursor").Should().BeFalse();

        var primera = json["sedes"]![0]!.AsObject();
        primera["codigo"]!.GetValue<string>().Should().Be("TEST-01a04653-70e8-77bd-8f99-342a693076ab");
        primera["nombre"]!.GetValue<string>().Should().Be("[TEST] Sede Norte");
        primera["ciudad"]!.GetValue<string>().Should().Be("Bogota");
        primera["direccion"]!.GetValue<string>().Should().Be("Calle 1 # 2-3");
        primera.ContainsKey("id").Should().BeFalse();
        primera.ContainsKey("dispositivos").Should().BeFalse();
        primera.ContainsKey("centroDeCostos").Should().BeFalse();
        primera.ContainsKey("activa").Should().BeFalse();

        var minima = json["sedes"]![1]!.AsObject();
        minima.ContainsKey("ciudad").Should().BeFalse();
        minima.ContainsKey("direccion").Should().BeFalse();
    }

    [Fact]
    public async Task ListarSedes_TraeSiguienteCursorYNota_CuandoElBackendTienePaginaSiguiente()
    {
        var sobre = JsonNode.Parse(Fixtures.Leer("ListarSedes", "listar-sedes.json"))!.AsObject();
        sobre["siguienteCursor"] = Cursor;

        var (json, _) = await Ejecutar(sobre.ToJsonString());

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json["sedes"]!.AsArray().Should().HaveCount(2);
        json["siguienteCursor"]!.GetValue<string>().Should().Be(Cursor);
        json["nota"]!.GetValue<string>().Should().Be(string.Format(ListarSedesTool.Mensajes.NotaHayMasSedes, Cursor));
        json.AsObject().ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ListarSedes_RespondeFraseDelCatalogo_CuandoNoHaySedesActivas()
    {
        var (cliente, _) = ClienteFalso.Con("""{"elementos":[]}""");

        var resultado = await new ListarSedesTool(new SedesApi(cliente))
            .Run(null!, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(ListarSedesTool.Mensajes.SinSedes);
    }

    [Fact]
    public async Task ListarSedes_RespondeTextoDeRechazo_CuandoElBackendResponde400()
    {
        var (cliente, _) = ClienteFalso.Con("El cursor es invalido", HttpStatusCode.BadRequest);

        var resultado = await new ListarSedesTool(new SedesApi(cliente))
            .Run(null!, "basura", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarSedesTool.Mensajes.RechazoDelDominio, "El cursor es invalido"));
    }

    [Fact]
    public async Task ListarSedes_RespondeTextoDeRechazo_CuandoElBackendResponde5xx()
    {
        var (cliente, _) = ClienteFalso.Con("", HttpStatusCode.BadGateway);

        var resultado = await new ListarSedesTool(new SedesApi(cliente))
            .Run(null!, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarSedesTool.Mensajes.RechazoDelDominio, "502"));
    }
}
