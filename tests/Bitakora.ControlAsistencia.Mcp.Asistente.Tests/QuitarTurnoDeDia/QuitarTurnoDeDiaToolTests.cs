using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.QuitarTurnoDeDia;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.QuitarTurnoDeDia;

public class QuitarTurnoDeDiaToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";

    // Reuso deliberado del fixture de plantillas de #627 (MEF-ADR-0018): mismo catalogo
    // (RetirarPlantillaSemanal, "Semana Tipo Cocina" id ...101).
    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");

    private sealed record Fakes(QuitarTurnoDeDiaTool Tool, HandlerPorRuta Handler);

    // Composicion por defecto del camino feliz (semana 1 por defecto, domingo = dia ISO 7) --
    // cada test sobreescribe unicamente el fixture/status que le interesa via argumentos nombrados.
    private static Fakes CrearTool(
        int semana = 1,
        int diaIso = 7,
        HttpStatusCode statusDetalle = HttpStatusCode.OK,
        HttpStatusCode statusDelete = HttpStatusCode.NoContent,
        string cuerpoDelete = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        if (statusDetalle == HttpStatusCode.OK)
            handler.RespondeDetalleDePlantilla(PlantillasJson, PlantillaIdCocina);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina), statusDetalle, "");
        handler.Responde(
            HttpMethod.Delete, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/{semana}/{diaIso}", statusDelete, cuerpoDelete);

        var programacionApi = new ProgramacionApi(cliente);
        var tool = new QuitarTurnoDeDiaTool(
            programacionApi, new ResolutorPlantillaPorId(programacionApi));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(
        QuitarTurnoDeDiaTool tool,
        string plantillaId = PlantillaIdCocina,
        string dia = "domingo",
        int? semana = null,
        CancellationToken ct = default) =>
        tool.Run(context: null!, plantillaId: plantillaId, dia: dia, semana: semana, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Handler.Requests.Should().BeEmpty();

    // CA-3: semana omitida -> 1 por defecto; lee el detalle de la plantilla con un GET (sin catalogo) y envia el DELETE al
    // dia exacto (domingo = ISO 7).
    [Fact]
    public async Task QuitarTurnoDeDia_LeeElDetalleYEnviaElDelete_CuandoSemanaSeOmite()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina)),
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

    // CA-3
    [Fact]
    public async Task QuitarTurnoDeDia_RespondePlantillaNoExisteSinDelete_Cuando404EnElDetalle()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.PlantillaNoExiste, PlantillaIdCocina));
        resultado.Should().Contain(PlantillaIdCocina).And.Contain("buscar_plantillas_semanales");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }

    // CA-2
    [Fact]
    public async Task QuitarTurnoDeDia_RechazaSinLlamarANingunDominio_CuandoPlantillaIdNoEsUnGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, plantillaId: "Semana Tipo Cocina", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.PlantillaIdInvalido, "Semana Tipo Cocina"));
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

    // El 400 de este DELETE es texto plano, no un ValidationProblemDetails: la ruta no lleva body,
    // asi que nunca pasa por RequestValidator (el PUT hermano si). Por eso no hay fixture JSON.
    [Fact]
    public async Task QuitarTurnoDeDia_TraduceElRechazoDelDominio_Cuando400()
    {
        const string cuerpo = "El id de la plantilla no es un Guid valido";
        var fakes = CrearTool(statusDelete: HttpStatusCode.BadRequest, cuerpoDelete: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    // Boundary del sistema: un 5xx del detalle de la plantilla se traduce a texto y corta antes de
    // cualquier DELETE (CA-ADR-0030).
    [Fact]
    public async Task QuitarTurnoDeDia_RespondeElRechazoDelDominioSinDelete_CuandoElDetalleDeLaPlantillaFalla()
    {
        var fakes = CrearTool(statusDetalle: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(QuitarTurnoDeDiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Delete);
    }
}
