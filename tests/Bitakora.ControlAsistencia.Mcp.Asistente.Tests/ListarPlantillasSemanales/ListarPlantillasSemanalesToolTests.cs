using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillasSemanales;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarPlantillasSemanales;

public class ListarPlantillasSemanalesToolTests
{
    private const string Cursor = "eyJOb21icmUiOiJBIiwiSWQiOiIxIn0";

    private static async Task<(JsonNode Json, HandlerEnlatado Handler)> Ejecutar(string cuerpo, string? cursor = null)
    {
        var (cliente, handler) = ClienteFalso.Con(cuerpo);
        var tool = new ListarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, cursor, TestContext.Current.CancellationToken);

        return (JsonNode.Parse(resultado)!, handler);
    }

    [Fact]
    public async Task ListarPlantillasSemanales_RemodelaCadaPlantillaAIdNombreYSemanas_CuandoElCatalogoResponde()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales.json"));

        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json.AsObject().ContainsKey("total").Should().BeFalse("CA-ADR-0039: sobre sin total");
        json.AsObject().ContainsKey("nota").Should().BeFalse();
        json.AsObject().ContainsKey("siguienteCursor").Should().BeFalse();

        var primera = json["plantillas"]![0]!.AsObject();
        primera["id"]!.GetValue<string>().Should().Be("01a07000-1000-7000-9000-000000000001");
        primera["nombre"]!.GetValue<string>().Should().Be("[TEST] Semana Cocina");
        primera["semanas"]!.GetValue<int>().Should().Be(1);
        primera.ContainsKey("incompleta").Should().BeFalse();
        primera.ContainsKey("conAdvertencias").Should().BeFalse();
    }

    [Fact]
    public async Task ListarPlantillasSemanales_MarcaIncompleta_CuandoLaPlantillaNoEstaCompleta()
    {
        var (json, _) = await Ejecutar(Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales.json"));

        json["plantillas"]![1]!["incompleta"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public async Task ListarPlantillasSemanales_MarcaConAdvertencias_CuandoLaPlantillaTraeAdvertencias()
    {
        var (json, _) = await Ejecutar(
            Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales-con-advertencias.json"));

        json["plantillas"]![0]!["conAdvertencias"]!.GetValue<bool>().Should().BeTrue();
        json["plantillas"]![1]!.AsObject().ContainsKey("conAdvertencias").Should().BeFalse(
            "la senal solo viaja cuando es true");
    }

    [Fact]
    public async Task ListarPlantillasSemanales_PideTakeIgualAlTopeSinCursor_CuandoNoRecibeCursor()
    {
        var (_, handler) = await Ejecutar(Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales.json"));

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/programacion/plantillas-semanales");
        var query = Uri.UnescapeDataString(handler.UltimaRequest.RequestUri.Query);
        query.Should().Contain("take=50").And.NotContain("cursor");
        ListarPlantillasSemanalesTool.MaximoPlantillas.Should().Be(50);
    }

    [Fact]
    public async Task ListarPlantillasSemanales_ReenviaElCursorTalCual_CuandoRecibeCursor()
    {
        var (_, handler) = await Ejecutar(
            Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales.json"), Cursor);

        Uri.UnescapeDataString(handler.UltimaRequest!.RequestUri!.Query).Should().Contain($"cursor={Cursor}");
    }

    [Fact]
    public async Task ListarPlantillasSemanales_TraeSiguienteCursorYNota_CuandoElBackendTienePaginaSiguiente()
    {
        var sobre = JsonNode.Parse(Fixtures.Leer("ListarPlantillasSemanales", "listar-plantillas-semanales.json"))!.AsObject();
        sobre["siguienteCursor"] = Cursor;

        var (json, _) = await Ejecutar(sobre.ToJsonString());

        json["mostrando"]!.GetValue<int>().Should().Be(4);
        json["plantillas"]!.AsArray().Should().HaveCount(4);
        json["siguienteCursor"]!.GetValue<string>().Should().Be(Cursor);
        json["nota"]!.GetValue<string>().Should()
            .Be(string.Format(ListarPlantillasSemanalesTool.Mensajes.NotaHayMasPlantillas, Cursor));
        json.AsObject().ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ListarPlantillasSemanales_RespondeFraseDelCatalogo_CuandoNoHayPlantillas()
    {
        var (cliente, _) = ClienteFalso.Con("""{"elementos":[]}""");
        var tool = new ListarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(ListarPlantillasSemanalesTool.Mensajes.SinPlantillas);
    }

    [Fact]
    public async Task ListarPlantillasSemanales_RespondeTextoDeRechazo_CuandoElBackendResponde400()
    {
        var (cliente, _) = ClienteFalso.Con("El cursor es invalido", HttpStatusCode.BadRequest);
        var tool = new ListarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, "basura", TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            ListarPlantillasSemanalesTool.Mensajes.RechazoDelDominio, "El cursor es invalido"));
    }

    [Fact]
    public async Task ListarPlantillasSemanales_RespondeTextoDeRechazo_CuandoElBackendResponde5xx()
    {
        var (cliente, _) = ClienteFalso.Con("", HttpStatusCode.InternalServerError);
        var tool = new ListarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var resultado = await tool.Run(null!, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ListarPlantillasSemanalesTool.Mensajes.RechazoDelDominio, "500"));
    }
}
