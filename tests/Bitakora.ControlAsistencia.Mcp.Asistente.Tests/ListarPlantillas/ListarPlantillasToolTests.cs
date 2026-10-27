using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.ListarPlantillas;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ListarPlantillas;

public class ListarPlantillasToolTests
{
    private const string Cursor = "cursor-opaco-1";

    private static string Catalogo(string? siguienteCursor = null) => Sobre(
    [
        Plantilla("id-p1", "  Ciclo   Cocina ", 14, true, [Dia(1, Turno("Cocina Manana"))]),
        Plantilla("id-p2", "Ciclo Aseo", 7, false, [], advertencias: 1)
    ], siguienteCursor);

    private static async Task<(string Texto, HandlerEnlatado Handler)> Ejecutar(
        string json, string? cursor = null, HttpStatusCode status = HttpStatusCode.OK)
    {
        var (cliente, handler) = ClienteFalso.Con(json, status);
        var tool = new ListarPlantillasTool(new ProgramacionApi(cliente));

        var texto = await tool.Run(null!, cursor, TestContext.Current.CancellationToken);

        return (texto, handler);
    }

    [Fact]
    public async Task ListarPlantillas_RemodelaIdNombreDiasDelCicloYMarcasSoloCuandoSonTrue_CuandoHayPlantillas()
    {
        var (texto, _) = await Ejecutar(Catalogo());
        var json = JsonNode.Parse(texto)!.AsObject();

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        var primera = json["plantillas"]![0]!.AsObject();
        primera["id"]!.GetValue<string>().Should().Be("id-p1");
        primera["nombre"]!.GetValue<string>().Should().Be("Ciclo   Cocina");
        primera["diasDelCiclo"]!.GetValue<int>().Should().Be(14);
        primera.ContainsKey("incompleta").Should().BeFalse();
        primera.ContainsKey("conAdvertencias").Should().BeFalse();

        var segunda = json["plantillas"]![1]!.AsObject();
        segunda["incompleta"]!.GetValue<bool>().Should().BeTrue();
        segunda["conAdvertencias"]!.GetValue<bool>().Should().BeTrue();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task ListarPlantillas_PideCincuentaYReenviaElCursor_CuandoSeLeeElCatalogo()
    {
        var (_, handler) = await Ejecutar(Catalogo(), cursor: " " + Cursor + " ");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaCatalogo);
        handler.UltimaRequest.RequestUri.Query.Should().Contain("take=50").And.Contain($"cursor={Cursor}");
    }

    [Fact]
    public async Task ListarPlantillas_TraeSiguienteCursorConNota_CuandoHayMasPaginas()
    {
        var (texto, _) = await Ejecutar(Catalogo(Cursor));
        var json = JsonNode.Parse(texto)!.AsObject();

        json["siguienteCursor"]!.GetValue<string>().Should().Be(Cursor);
        json["nota"]!.GetValue<string>().Should().Be(string.Format(ListarPlantillasTool.Mensajes.NotaHayMasPlantillas, Cursor));
    }

    [Fact]
    public async Task ListarPlantillas_RespondeMensaje_CuandoElCatalogoEstaVacio()
    {
        var (texto, _) = await Ejecutar(Sobre([]));

        texto.Should().Be(ListarPlantillasTool.Mensajes.SinPlantillas);
    }

    [Fact]
    public async Task ListarPlantillas_RespondeRechazoDelDominio_CuandoElBackendFalla()
    {
        var (texto, _) = await Ejecutar("cursor invalido", status: HttpStatusCode.BadRequest);

        texto.Should().Be(string.Format(ListarPlantillasTool.Mensajes.RechazoDelDominio, "cursor invalido"));
    }
}
