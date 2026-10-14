using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarPlantillasSemanales;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarPlantillasSemanales;

public class BuscarPlantillasSemanalesToolTests
{
    private static async Task<(string Texto, HandlerEnlatado Handler)> EjecutarTexto(
        string json, string? nombre = null, string? turno = null, string? semanas = null, string? estado = null)
    {
        var (cliente, handler) = ClienteFalso.Con(json);
        var tool = new BuscarPlantillasSemanalesTool(new ProgramacionApi(cliente));

        var texto = await tool.Run(null!, nombre, turno, semanas, estado, TestContext.Current.CancellationToken);

        return (texto, handler);
    }

    private static string Catalogo => Fixtures.Leer("BuscarPlantillasSemanales", "buscar-plantillas-semanales.json");

    private static JsonObject Parsear(string texto) => JsonNode.Parse(texto)!.AsObject();

    private static IEnumerable<string> Nombres(JsonObject json) =>
        json["plantillas"]!.AsArray().Select(p => p!["nombre"]!.GetValue<string>());

    private static JsonObject Plantilla(JsonObject json, string nombre) =>
        json["plantillas"]!.AsArray().Select(p => p!.AsObject())
            .Single(p => p["nombre"]!.GetValue<string>() == nombre);

    private static IEnumerable<string> Motivos(JsonObject plantilla) =>
        plantilla["coincidePor"]!.AsArray().Select(m => m!.GetValue<string>());

    [Fact]
    public async Task BuscarPlantillasSemanales_FiltraPorNombreAproximado_CuandoSePidenPalabrasSinAcentosOConUnaLetraDeDiferencia()
    {
        var (exacto, _) = await EjecutarTexto(Catalogo, nombre: "VIGILANCIA");
        var (conTypo, _) = await EjecutarTexto(Catalogo, nombre: "vigilansia");

        Nombres(Parsear(exacto)).Should().Equal("Vigilancia Nocturna", "Vigilancia Norte");
        Nombres(Parsear(conTypo)).Should().Equal("Vigilancia Nocturna", "Vigilancia Norte");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_PoneMasPalabrasPrimero_CuandoElNombreTieneVariasCoincidencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia norte");

        Nombres(Parsear(texto)).Should().Equal("Vigilancia Norte", "Vigilancia Nocturna");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_FiltraPorTurnoAproximado_CuandoAlgunDiaUsaElTurno()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, turno: "cocina manana");

        Nombres(Parsear(texto)).Should().Equal("Cocina Semanal", "Vigilancia Nocturna");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_ReportaLaPrimeraAparicionDelTurno_CuandoElTurnoSeRepite()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, turno: "vigilancia diurno");
        var json = Parsear(texto);

        Motivos(Plantilla(json, "Vigilancia Norte")).Should().Contain(
            string.Format(BuscarPlantillasSemanalesTool.Mensajes.CoincidePorTurno, "Vigilancia Diurno", 1, "lunes"));
        Motivos(Plantilla(json, "Vigilancia Nocturna")).Should().Contain(
            string.Format(BuscarPlantillasSemanalesTool.Mensajes.CoincidePorTurno, "Vigilancia Diurno", 2, "miercoles"));
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_FiltraPorIgualdadDeSemanas_CuandoSePideUnNumero()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, semanas: "4");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Vigilancia Nocturna", "Vigilancia Norte");
        Motivos(Plantilla(json, "Vigilancia Norte")).Should().Contain(
            string.Format(BuscarPlantillasSemanalesTool.Mensajes.CoincidePorSemanas, 4));
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_DejaLasIncompletas_CuandoElEstadoEsIncompleta()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, estado: "incompleta");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Vigilancia Nocturna");
        Motivos(Plantilla(json, "Vigilancia Nocturna")).Should().Contain(
            BuscarPlantillasSemanalesTool.Mensajes.CoincidePorIncompleta);
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_DejaLasQueTienenAdvertencias_CuandoElEstadoEsConAdvertenciasIncluidaLaSinJornada()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, estado: "con_advertencias");
        var json = Parsear(texto);

        Nombres(json).Should().Equal("Cocina Semanal", "Vigilancia Nocturna");
        Motivos(Plantilla(json, "Cocina Semanal")).Should().Contain(
            BuscarPlantillasSemanalesTool.Mensajes.CoincidePorConAdvertencias);
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_CombinaLosCriteriosEnAnd_CuandoHayVarios()
    {
        var (porNombreYEstado, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia", estado: "incompleta");
        var (porTurnoYSemanas, _) = await EjecutarTexto(Catalogo, turno: "cocina", semanas: "4");

        Nombres(Parsear(porNombreYEstado)).Should().Equal("Vigilancia Nocturna");
        Nombres(Parsear(porTurnoYSemanas)).Should().Equal("Vigilancia Nocturna");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_TraeIdNombreSemanasYMarcasSoloCuandoAplican_CuandoRemodela()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "vigilancia");
        var json = Parsear(texto);

        var limpia = Plantilla(json, "Vigilancia Norte");
        limpia["id"]!.GetValue<string>().Should().Be("id-p1");
        limpia["semanas"]!.GetValue<int>().Should().Be(4);
        limpia.ContainsKey("incompleta").Should().BeFalse();
        limpia.ContainsKey("advertencias").Should().BeFalse();

        var marcada = Plantilla(json, "Vigilancia Nocturna");
        marcada["incompleta"]!.GetValue<bool>().Should().BeTrue();
        marcada["advertencias"]!.GetValue<int>().Should().Be(2);

        json["mostrando"]!.GetValue<int>().Should().Be(2);
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_ReportaElNumeroDeAdvertencias_CuandoLaPlantillaNoTieneJornada()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "cocina");

        Plantilla(Parsear(texto), "Cocina Semanal")["advertencias"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_ConsumeElListadoSinTake_CuandoBusca()
    {
        var (_, handler) = await EjecutarTexto(Catalogo, nombre: "vigilancia");

        handler.UltimaRequest!.RequestUri!.AbsolutePath.Should().Be("/api/programacion/plantillas-semanales");
        handler.UltimaRequest.RequestUri.Query.Should().NotContain("take");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_RespondeConFraseSinLlamarAlServidor_CuandoNoHayCriterios()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, "  ", null, "", " ");

        texto.Should().Be(BuscarPlantillasSemanalesTool.Mensajes.FaltaCriterio);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_RespondeConFraseSinLlamarAlServidor_CuandoLasSemanasNoEstanEntreUnoYSeis()
    {
        foreach (var semanas in new[] { "7", "0", "dos" })
        {
            var (texto, handler) = await EjecutarTexto(Catalogo, semanas: semanas);

            texto.Should().Be(string.Format(BuscarPlantillasSemanalesTool.Mensajes.SemanasInvalidas, semanas));
            handler.UltimaRequest.Should().BeNull();
        }
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_RespondeConFraseSinLlamarAlServidor_CuandoElEstadoEsDesconocido()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, estado: "rota");

        texto.Should().Be(string.Format(BuscarPlantillasSemanalesTool.Mensajes.EstadoDesconocido, "rota"));
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_RespondeConFraseConElCriterio_CuandoNadaCoincide()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "zzzqq");

        texto.Should().Contain("zzzqq");
        texto.Should().NotStartWith("{");
    }

    [Fact]
    public async Task BuscarPlantillasSemanales_LimitaAVeinteYAvisa_CuandoHayMasCoincidencias()
    {
        var elementos = Enumerable.Range(1, 25).Select(i =>
            $$"""{"id":"id-{{i}}","nombre":"Vigilancia {{i:00}}","semanas":1,"completa":true,"dias":[]}""");
        var json = $$"""{"elementos":[{{string.Join(",", elementos)}}]}""";

        var (texto, _) = await EjecutarTexto(json, nombre: "vigilancia");
        var respuesta = Parsear(texto);

        respuesta["mostrando"]!.GetValue<int>().Should().Be(20);
        respuesta["plantillas"]!.AsArray().Should().HaveCount(20);
        respuesta["nota"]!.GetValue<string>().Should().Be(BuscarPlantillasSemanalesTool.Mensajes.NotaTruncado);
        respuesta.ContainsKey("siguienteCursor").Should().BeFalse();
        respuesta.ContainsKey("total").Should().BeFalse();
    }
}
