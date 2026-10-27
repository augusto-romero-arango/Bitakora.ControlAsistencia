using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillas;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarPlantillas;

public class BuscarPlantillasToolTests
{
    private static string Catalogo => Sobre(
    [
        Plantilla("id-p1", "Ciclo Vigilancia Norte", 14, true,
            [Dia(1, Turno("Vigilancia Diurno"))]),
        Plantilla("id-p2", "Ciclo Vigilancia Nocturna", 28, false,
            [Dia(2, Turno("Cocina Manana")), Dia(3, Turno("Vigilancia Diurno"))], advertencias: 2),
        Plantilla("id-p3", "Cocina Rotativa", 7, true,
            [Dia(1, Turno("Cocina Manana"))], advertencias: 1, conJornada: false)
    ]);

    private static async Task<(string Texto, HandlerEnlatado Handler)> EjecutarTexto(
        string json, string? nombre = null, string? turno = null, string? dias = null, string? estado = null)
    {
        var (cliente, handler) = ClienteFalso.Con(json);
        var tool = new BuscarPlantillasTool(new ProgramacionApi(cliente));

        var texto = await tool.Run(null!, nombre, turno, dias, estado, TestContext.Current.CancellationToken);

        return (texto, handler);
    }

    private static JsonObject Parsear(string texto) => JsonNode.Parse(texto)!.AsObject();

    private static IEnumerable<string> Nombres(JsonObject json) =>
        json["plantillas"]!.AsArray().Select(p => p!["nombre"]!.GetValue<string>());

    private static JsonObject Plantilla_(JsonObject json, string nombre) =>
        json["plantillas"]!.AsArray().Select(p => p!.AsObject())
            .Single(p => p["nombre"]!.GetValue<string>() == nombre);

    private static IEnumerable<string> Motivos(JsonObject plantilla) =>
        plantilla["coincidePor"]!.AsArray().Select(m => m!.GetValue<string>());

    [Fact]
    public async Task BuscarPlantillas_FiltraPorNombreAproximado_CuandoSePidenPalabrasSinAcentosOConUnaLetraDeDiferencia()
    {
        var (exacto, _) = await EjecutarTexto(Catalogo, nombre: "VIGILANCIA");
        var (conTypo, _) = await EjecutarTexto(Catalogo, nombre: "vigilansia");

        Nombres(Parsear(exacto)).Should().Equal("Ciclo Vigilancia Nocturna", "Ciclo Vigilancia Norte");
        Nombres(Parsear(conTypo)).Should().Equal("Ciclo Vigilancia Nocturna", "Ciclo Vigilancia Norte");
    }

    [Fact]
    public async Task BuscarPlantillas_PoneMasPalabrasPrimero_CuandoElNombreTieneVariasCoincidencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia norte");

        Nombres(Parsear(texto)).Should().Equal("Ciclo Vigilancia Norte", "Ciclo Vigilancia Nocturna");
    }

    [Fact]
    public async Task BuscarPlantillas_FiltraPorTurnoAproximado_CuandoAlgunDiaUsaElTurno()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, turno: "cocina manana");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Ciclo Vigilancia Nocturna", "Cocina Rotativa");
        Motivos(Plantilla_(json, "Ciclo Vigilancia Nocturna")).Should().Contain(
            string.Format(BuscarPlantillasTool.Mensajes.CoincidePorTurno, "Cocina Manana", 2));
    }

    [Fact]
    public async Task BuscarPlantillas_FiltraPorIgualdadDeDiasDelCiclo_CuandoSePideUnNumero()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, dias: "28");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Ciclo Vigilancia Nocturna");
        Motivos(Plantilla_(json, "Ciclo Vigilancia Nocturna")).Should().Contain(
            string.Format(BuscarPlantillasTool.Mensajes.CoincidePorDias, 28));
    }

    [Fact]
    public async Task BuscarPlantillas_DejaLasIncompletas_CuandoElEstadoEsIncompleta()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, estado: "incompleta");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Ciclo Vigilancia Nocturna");
        Motivos(Plantilla_(json, "Ciclo Vigilancia Nocturna")).Should().Contain(
            BuscarPlantillasTool.Mensajes.CoincidePorIncompleta);
    }

    [Fact]
    public async Task BuscarPlantillas_DejaLasQueTienenAdvertencias_CuandoElEstadoEsConAdvertencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, estado: "con_advertencias");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Ciclo Vigilancia Nocturna", "Cocina Rotativa");
        Motivos(Plantilla_(json, "Cocina Rotativa")).Should().Contain(
            BuscarPlantillasTool.Mensajes.CoincidePorConAdvertencias);
    }

    [Fact]
    public async Task BuscarPlantillas_CombinaLosCriteriosEnAnd_CuandoHayVarios()
    {
        var (porNombreYEstado, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia", estado: "incompleta");
        var (porTurnoYDias, _) = await EjecutarTexto(Catalogo, turno: "cocina", dias: "28");

        Nombres(Parsear(porNombreYEstado)).Should().Equal("Ciclo Vigilancia Nocturna");
        Nombres(Parsear(porTurnoYDias)).Should().Equal("Ciclo Vigilancia Nocturna");
    }

    [Fact]
    public async Task BuscarPlantillas_TraeIdNombreDiasDelCicloYMarcasSoloCuandoAplican_CuandoRemodela()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia");
        var json = Parsear(texto);

        var limpia = Plantilla_(json, "Ciclo Vigilancia Norte");
        limpia["id"]!.GetValue<string>().Should().Be("id-p1");
        limpia["diasDelCiclo"]!.GetValue<int>().Should().Be(14);
        limpia.ContainsKey("incompleta").Should().BeFalse();
        limpia.ContainsKey("advertencias").Should().BeFalse();

        var marcada = Plantilla_(json, "Ciclo Vigilancia Nocturna");
        marcada["incompleta"]!.GetValue<bool>().Should().BeTrue();
        marcada["advertencias"]!.GetValue<int>().Should().Be(2);

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarPlantillas_ConsumeElCatalogoSinTake_CuandoBusca()
    {
        var (_, handler) = await EjecutarTexto(Catalogo, nombre: "vigilancia");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be(RutaCatalogo);
        handler.UltimaRequest.RequestUri.Query.Should().NotContain("take");
    }

    [Fact]
    public async Task BuscarPlantillas_DevuelveVeinteConNota_CuandoHayMasDeVeinteCoincidencias()
    {
        var muchas = Enumerable.Range(1, 25)
            .Select(i => Plantilla($"id-{i}", $"Plantilla {i:00}", 7, true, [])).ToArray();

        var (texto, _) = await EjecutarTexto(Sobre(muchas), nombre: "plantilla");
        var json = Parsear(texto);

        json["mostrando"]!.GetValue<int>().Should().Be(20);
        json["plantillas"]!.AsArray().Should().HaveCount(20);
        json["nota"]!.GetValue<string>().Should().Be(BuscarPlantillasTool.Mensajes.NotaTruncado);
    }

    [Fact]
    public async Task BuscarPlantillas_RespondeConFraseSinLlamarAlServidor_CuandoNoHayCriterios()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, "  ", null, "", " ");

        texto.Should().Be(BuscarPlantillasTool.Mensajes.FaltaCriterio);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarPlantillas_RespondeConFraseSinLlamarAlServidor_CuandoLosDiasNoEstanEntreUnoYCuarentaYDos()
    {
        foreach (var dias in new[] { "43", "0", "dos" })
        {
            var (texto, handler) = await EjecutarTexto(Catalogo, dias: dias);

            texto.Should().Be(string.Format(BuscarPlantillasTool.Mensajes.DiasInvalidos, dias));
            handler.UltimaRequest.Should().BeNull();
        }
    }

    [Fact]
    public async Task BuscarPlantillas_RespondeConFraseSinLlamarAlServidor_CuandoElEstadoEsDesconocido()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, estado: "rota");

        texto.Should().Be(string.Format(BuscarPlantillasTool.Mensajes.EstadoDesconocido, "rota"));
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarPlantillas_RespondeConFraseConElCriterio_CuandoNadaCoincide()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "zzzzzz");

        texto.Should().Be(string.Format(
            BuscarPlantillasTool.Mensajes.SinCoincidencias,
            string.Format(BuscarPlantillasTool.Mensajes.CriterioNombre, "zzzzzz")));
    }
}
