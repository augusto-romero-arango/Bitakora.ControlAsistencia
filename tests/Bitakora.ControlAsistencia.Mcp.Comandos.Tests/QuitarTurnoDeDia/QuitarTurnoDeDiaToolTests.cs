using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.QuitarTurnoDeDia;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.QuitarTurnoDeDia;

public class QuitarTurnoDeDiaToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";

    // Reuso deliberado del fixture de plantillas de #627 (MEF-ADR-0018): mismo catalogo
    // (RetirarPlantillaSemanal, "Semana Tipo Cocina" id ...101).
    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");
    private static string Validacion400Json => Fixtures.Leer("QuitarTurnoDeDia", "validacion-400.json");

    private sealed record Fakes(QuitarTurnoDeDiaTool Tool, HandlerPorRuta Handler);

    // Composicion por defecto del camino feliz (semana 1 por defecto, domingo = dia ISO 7) --
    // cada test sobreescribe unicamente el fixture/status que le interesa via argumentos nombrados.
    private static Fakes CrearTool(
        int semana = 1,
        int diaIso = 7,
        string? plantillasJson = null,
        HttpStatusCode statusPlantillas = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaPlantillas, statusPlantillas, plantillasJson ?? PlantillasJson);
        handler.Responde(
            HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/{semana}/{diaIso}", statusDelete, cuerpoDelete);

        var tool = new QuitarTurnoDeDiaTool(new ProgramacionApi(cliente));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(
        QuitarTurnoDeDiaTool tool,
        string plantilla = "Semana Tipo Cocina",
        string dia = "domingo",
        int? semana = null,
        CancellationToken ct = default) =>
        tool.Run(context: null!, plantilla: plantilla, dia: dia, semana: semana, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Handler.Requests.Should().BeEmpty();

    // CA-3: semana omitida -> 1 por defecto; resuelve la plantilla con un GET y envia el DELETE al
    // dia exacto (domingo = ISO 7).
    [Fact]
    public async Task QuitarTurnoDeDia_ResuelvePlantillaYEnviaElDelete_CuandoSemanaSeOmite()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaPlantillas),
            (HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/1/7"));

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(QuitarTurnoDeDiaTool.Mensajes.ResultadoTurnoQuitado);
        json["plantilla"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["semana"]!.GetValue<int>().Should().Be(1);
        json["dia"]!.GetValue<string>().Should().Be("domingo");
        json["nota"]!.GetValue<string>().Should().Be(QuitarTurnoDeDiaTool.Mensajes.NotaVisibilidadEventual);
    }

    // CA-3: un 204 sobre un dia ya vacio (idempotente, #622/harness#850) se reporta como el mismo
    // exito -- el dominio no distingue, la tool tampoco.
    [Fact]
    public async Task QuitarTurnoDeDia_RespondeElMismoEcoDeExito_CuandoElDiaYaEstabaVacio()
    {
        var fakes = CrearTool(statusDelete: HttpStatusCode.NoContent);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        JsonNode.Parse(resultado)!["resultado"]!.GetValue<string>()
            .Should().Be(QuitarTurnoDeDiaTool.Mensajes.ResultadoTurnoQuitado);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RespondePlantillaNoExisteSinDelete_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, plantilla: "Plantilla Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            QuitarTurnoDeDiaTool.Mensajes.PlantillaNoExiste,
            "Plantilla Que No Existe", "Semana Tipo Cocina, Turno Rotativo"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoPlantillaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, plantilla: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.CampoObligatorio, "plantilla"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoDiaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.CampoObligatorio, "dia"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoElDiaEsUnNombreDesconocido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "funes", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.DiaDesconocido, "funes"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoElDiaEsCero()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "0", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.DiaDesconocido, "0"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoElDiaEsOcho()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "8", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.DiaDesconocido, "8"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoLaSemanaEsMenorAUno()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, semana: 0, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.SemanaInvalida, 0));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task QuitarTurnoDeDia_TraduceElRechazoDelDominio_Cuando409PorSemanaFueraDeRango()
    {
        const string cuerpo = "La semana esta fuera de rango";
        var fakes = CrearTool(statusDelete: HttpStatusCode.Conflict, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarTurnoDeDia_TraduceElRechazoDelDominio_Cuando404()
    {
        const string cuerpo = "La plantilla no existe";
        var fakes = CrearTool(statusDelete: HttpStatusCode.NotFound, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task QuitarTurnoDeDia_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusDelete: HttpStatusCode.BadRequest, cuerpoDelete: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, fixture));
    }

    // Boundary del sistema: un 5xx del catalogo de plantillas se traduce a texto y corta antes de
    // cualquier DELETE (CA-ADR-0030).
    [Fact]
    public async Task QuitarTurnoDeDia_RespondeElRechazoDelDominioSinDelete_CuandoElCatalogoDePlantillasFalla()
    {
        var fakes = CrearTool(plantillasJson: "", statusPlantillas: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
