using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.BuscarSedes;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.BuscarSedes;

public class BuscarSedesToolTests
{
    private static async Task<(string Texto, HandlerEnlatado Handler)> EjecutarTexto(
        string json, string? nombre = null, string? ciudad = null, string? direccion = null)
    {
        var (cliente, handler) = ClienteFalso.Con(json);
        var tool = new BuscarSedesTool(new SedesApi(cliente));

        var texto = await tool.Run(null!, nombre, ciudad, direccion, TestContext.Current.CancellationToken);

        return (texto, handler);
    }

    private static string Catalogo => Fixtures.Leer("BuscarSedes", "buscar-sedes.json");

    private static JsonObject Parsear(string texto) => JsonNode.Parse(texto)!.AsObject();

    private static IEnumerable<string> Codigos(JsonObject json) =>
        json["sedes"]!.AsArray().Select(s => s!["codigo"]!.GetValue<string>());

    private static JsonObject Sede(JsonObject json, string codigo) =>
        json["sedes"]!.AsArray().Single(s => s!["codigo"]!.GetValue<string>() == codigo)!.AsObject();

    private static IEnumerable<string> Motivos(JsonObject sede) =>
        sede["coincidePor"]!.AsArray().Select(m => m!.GetValue<string>());

    [Fact]
    public async Task BuscarSedes_EncuentraPorNombreIncompleto_CuandoSePideChapi()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "chapi");
        var json = Parsear(texto);

        Codigos(json).First().Should().Be("CH-01");
        Motivos(Sede(json, "CH-01")).Should().Contain(
            string.Format(
                BuscarSedesTool.Mensajes.CoincidePorNombre,
                string.Format(BuscarSedesTool.Mensajes.PalabraAproximada, "chapi", "chapinero")));
    }

    [Fact]
    public async Task BuscarSedes_EncuentraPorCodigo_CuandoElNombreNoLoContiene()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "chap");
        var json = Parsear(texto);

        Codigos(json).Should().Contain(["CH-01", "CHAP"]);
        Motivos(Sede(json, "CHAP")).Should().Contain(
            string.Format(BuscarSedesTool.Mensajes.CoincidePorCodigo, "CHAP"));
    }

    [Fact]
    public async Task BuscarSedes_EncuentraPorCiudadSinAcentos_CuandoSePideMedellin()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, ciudad: "medellin");
        var json = Parsear(texto);

        Codigos(json).Should().Equal("MED-02", "CHAP");
        Motivos(Sede(json, "CHAP")).Should().Equal(
            string.Format(BuscarSedesTool.Mensajes.CoincidePorCiudad, "medellin"));
    }

    [Fact]
    public async Task BuscarSedes_EncuentraPorDireccion_CuandoSePideCalle80()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, direccion: "calle 80");
        var json = Parsear(texto);

        Codigos(json).Should().Equal("BOG-80", "CHAP");
        Motivos(Sede(json, "BOG-80")).Should().Equal(
            string.Format(BuscarSedesTool.Mensajes.CoincidePorDireccion, "calle 80"));
    }

    [Fact]
    public async Task BuscarSedes_ExcluyeLaSedeSinCiudad_CuandoSePideCiudad()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, ciudad: "bogota");

        Codigos(Parsear(texto)).Should().Equal("BOG-80", "CH-01");
    }

    [Fact]
    public async Task BuscarSedes_CombinaLosCriteriosEnAnd_CuandoVienenLosTres()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, "sede", "medellin", "calle 80");
        var json = Parsear(texto);

        Codigos(json).Should().Equal("CHAP");
        Motivos(Sede(json, "CHAP")).Should().HaveCount(3);
    }

    [Fact]
    public async Task BuscarSedes_OrdenaPorNombreAlfabetico_CuandoLaCalidadEsIgual()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "sede");

        Codigos(Parsear(texto)).Should().Equal("BOG-80", "CH-01", "MED-02", "CHAP", "SIN-01");
    }

    [Fact]
    public async Task BuscarSedes_PriorizaLaDeMasPalabrasCoincidentes_CuandoElNombreTieneVarias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "sede chapinero");

        Codigos(Parsear(texto)).Should().Equal("CH-01", "BOG-80", "MED-02", "CHAP", "SIN-01");
    }

    [Fact]
    public async Task BuscarSedes_OmiteCiudadYDireccion_CuandoLaSedeNoLasTiene()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "sin");
        var json = Parsear(texto);

        var sede = Sede(json, "SIN-01");
        sede["nombre"]!.GetValue<string>().Should().Be("Sede Sin Ciudad");
        sede.ContainsKey("ciudad").Should().BeFalse();
        sede.ContainsKey("direccion").Should().BeFalse();
        json["mostrando"]!.GetValue<int>().Should().Be(1);
        json.ContainsKey("nota").Should().BeFalse();
        json.ContainsKey("siguienteCursor").Should().BeFalse();
        json.ContainsKey("total").Should().BeFalse();
    }

    [Fact]
    public async Task BuscarSedes_TraeCodigoNombreCiudadYDireccion_CuandoLaSedeLosTiene()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "chapinero");

        var sede = Sede(Parsear(texto), "CH-01");
        sede["nombre"]!.GetValue<string>().Should().Be("Sede Chapinero");
        sede["ciudad"]!.GetValue<string>().Should().Be("Bogotá");
        sede["direccion"]!.GetValue<string>().Should().Be("Carrera 13 # 63-20");
    }

    [Fact]
    public async Task BuscarSedes_ConsumeLasFichasActivasSinTake_CuandoBusca()
    {
        var (_, handler) = await EjecutarTexto(Catalogo, nombre: "sede");

        handler.UltimaRequest!.RequestUri!.PathAndQuery.Should().Be("/api/sedes/fichas?activa=true");
    }

    [Fact]
    public async Task BuscarSedes_RespondeConFraseSinLlamarAlServidor_CuandoNoHayCriterios()
    {
        var (texto, handler) = await EjecutarTexto(Catalogo, "  ", null, "");

        texto.Should().Be(BuscarSedesTool.Mensajes.FaltaCriterio);
        handler.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task BuscarSedes_RespondeConFraseDelCriterio_CuandoNoHayCoincidencias()
    {
        var (texto, _) = await EjecutarTexto(Catalogo, nombre: "zzzzzz", ciudad: "yyyyyy");

        texto.Should().Be(string.Format(
            BuscarSedesTool.Mensajes.SinCoincidencias,
            string.Join(", ",
                string.Format(BuscarSedesTool.Mensajes.CriterioNombre, "zzzzzz"),
                string.Format(BuscarSedesTool.Mensajes.CriterioCiudad, "yyyyyy"))));
    }

    [Fact]
    public async Task BuscarSedes_TruncaAVeinteConNota_CuandoHayVeinticincoCoincidencias()
    {
        var elementos = Enumerable.Range(1, 25).Select(i => new
        {
            id = $"s:SED-{i:00}",
            codigo = $"SED-{i:00}",
            nombre = $"Sede Norte {i:00}",
            ciudad = "Bogota",
            direccion = (string?)null,
            centroDeCostos = (string?)null,
            activa = true,
            dispositivos = Array.Empty<string>()
        });
        var json = JsonSerializer.Serialize(new { elementos });

        var (texto, _) = await EjecutarTexto(json, nombre: "norte");
        var raiz = Parsear(texto);

        raiz["mostrando"]!.GetValue<int>().Should().Be(20);
        raiz["sedes"]!.AsArray().Should().HaveCount(20);
        raiz["nota"]!.GetValue<string>().Should().Be(BuscarSedesTool.Mensajes.NotaTruncado);
        raiz.ContainsKey("siguienteCursor").Should().BeFalse();
        raiz.ContainsKey("total").Should().BeFalse();
    }
}
