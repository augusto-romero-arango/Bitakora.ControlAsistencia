using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADiaDePlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CatalogoDePlantillasTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarTurnoADiaDePlantilla;

public class AsignarTurnoADiaDePlantillaToolTests
{
    private const string PlantillaId = "01a07000-1000-7000-9000-000000000001";
    private const string TurnoIdCocinaManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");

    private sealed record Fakes(AsignarTurnoADiaDePlantillaTool Tool, HandlerPorRuta Handler);

    private static Fakes CrearTool(
        HttpStatusCode statusPlantilla = HttpStatusCode.OK,
        HttpStatusCode statusTurno = HttpStatusCode.OK,
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(
            HttpMethod.Get, RutaDetalle(PlantillaId), statusPlantilla,
            statusPlantilla == HttpStatusCode.OK
                ? Plantilla(PlantillaId, "Ciclo Cocina", 7, true, [Dia(1, Turno("Cocina Manana"))])
                : "");
        if (statusTurno == HttpStatusCode.OK)
            handler.RespondeDetalleDeTurno(TurnosJson, TurnoIdCocinaManana);
        else
            handler.Responde(HttpMethod.Get, DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana), statusTurno, "");
        handler.Responde(HttpMethod.Put, $"{RutaCatalogo}/{PlantillaId}/dias/3", statusPut, cuerpoPut);

        var api = new ProgramacionApi(cliente);
        var tool = new AsignarTurnoADiaDePlantillaTool(
            api, new ResolutorPlantillaTurnosPorId(api), new ResolutorTurnoPorId(api));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(
        Fakes fakes, string plantillaId = PlantillaId, int? dia = 3, string turnoId = TurnoIdCocinaManana) =>
        fakes.Tool.Run(null!, plantillaId, dia, turnoId, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_LeeLosDetallesYEnviaElPut_CuandoAmbosExisten()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaDetalle(PlantillaId)),
            (HttpMethod.Get, DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana)),
            (HttpMethod.Put, $"{RutaCatalogo}/{PlantillaId}/dias/3"));
        JsonNode.Parse(fakes.Handler.Requests[2].Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoIdCocinaManana);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarTurnoADiaDePlantillaTool.Mensajes.ResultadoTurnoAsignado);
        json["plantilla"]!.GetValue<string>().Should().Be("Ciclo Cocina");
        json["dia"]!.GetValue<int>().Should().Be(3);
        json["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["nota"]!.GetValue<string>().Should().Be(AsignarTurnoADiaDePlantillaTool.Mensajes.NotaVisibilidadEventual);
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RespondePlantillaNoExisteSinPut_Cuando404EnElDetalleDeLaPlantilla()
    {
        var fakes = CrearTool(statusPlantilla: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.PlantillaNoExiste, PlantillaId));
        resultado.Should().Contain("buscar_plantillas");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RespondeTurnoNoExisteSinPut_Cuando404EnElDetalleDelTurno()
    {
        var fakes = CrearTool(statusTurno: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.TurnoNoExiste, TurnoIdCocinaManana));
        resultado.Should().Contain("buscar_turnos");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RechazaSinLlamadasHttp_CuandoPlantillaIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, plantillaId: "Ciclo Cocina");

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo Cocina"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RechazaSinLlamadasHttp_CuandoTurnoIdNoEsGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, turnoId: "Cocina Manana");

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.TurnoIdInvalido, "Cocina Manana"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RechazaSinLlamadasHttp_CuandoElDiaFalta()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, dia: null);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.CampoObligatorio, "dia"));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RechazaSinLlamadasHttp_CuandoElDiaEsMenorAUno()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes, dia: 0);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.DiaInvalido, 0));
        fakes.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_TraduceElRechazoDelDominio_Cuando409PorDiaFueraDelCiclo()
    {
        const string cuerpo = "El dia esta fuera del ciclo";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_TraduceElRechazoDelDominio_Cuando409PorTurnoIncompleto()
    {
        const string cuerpo = "El turno esta incompleto";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarTurnoADiaDePlantilla_RespondeElRechazoDelDominioSinPut_CuandoElDetalleDeLaPlantillaFalla()
    {
        var fakes = CrearTool(statusPlantilla: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes);

        resultado.Should().Be(string.Format(AsignarTurnoADiaDePlantillaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
