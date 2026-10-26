using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarTurnoADia;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarTurnoADia;

public class AsignarTurnoADiaToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string PlantillaIdCocina = "8f14e45f-ceea-4b3c-8f0a-000000000101";
    private const string TurnoIdCocinaManana = "8f14e45f-ceea-4b3c-8f0a-000000000001";

    // Reuso deliberado de los fixtures de #627 (MEF-ADR-0018): mismo catalogo de plantillas
    // (RetirarPlantillaSemanal, "Semana Tipo Cocina" id ...101) y de turnos
    // (SolicitarProgramacionTurno, "Cocina Manana" id ...001, "Cocina Tarde" id ...002).
    private static string PlantillasJson => Fixtures.Leer("RetirarPlantillaSemanal", "plantillas-semanales.json");
    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");
    private static string Validacion400Json => Fixtures.Leer("AsignarTurnoADia", "validacion-400.json");

    private sealed record Fakes(AsignarTurnoADiaTool Tool, HandlerPorRuta Handler);

    // Composicion por defecto del camino feliz (semana 2, miercoles = dia ISO 3) -- cada test
    // sobreescribe unicamente el fixture/status que le interesa via argumentos nombrados.
    private static Fakes CrearTool(
        int semana = 2,
        int diaIso = 3,
        HttpStatusCode statusPlantilla = HttpStatusCode.OK,
        HttpStatusCode statusTurno = HttpStatusCode.OK,
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        if (statusPlantilla == HttpStatusCode.OK)
            handler.RespondeDetalleDePlantilla(PlantillasJson, PlantillaIdCocina);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina), statusPlantilla, "");
        if (statusTurno == HttpStatusCode.OK)
            handler.RespondeDetalleDeTurno(TurnosJson, TurnoIdCocinaManana);
        else
            handler.Responde(
                HttpMethod.Get, DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana), statusTurno, "");
        handler.Responde(
            HttpMethod.Put, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/{semana}/{diaIso}", statusPut, cuerpoPut);

        var programacionApi = new ProgramacionApi(cliente);
        var tool = new AsignarTurnoADiaTool(
            programacionApi, new ResolutorPlantillaPorId(programacionApi), new ResolutorTurnoPorId(programacionApi));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(
        AsignarTurnoADiaTool tool,
        string plantillaId = PlantillaIdCocina,
        string turnoId = TurnoIdCocinaManana,
        string dia = "miercoles",
        int? semana = 2,
        CancellationToken ct = default) =>
        tool.Run(context: null!, plantillaId: plantillaId, turnoId: turnoId, dia: dia, semana: semana, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Handler.Requests.Should().BeEmpty();

    // CA-1: lee el detalle de plantilla y de turno (sin catalogos), luego el PUT al dia exacto con el
    // turnoId resuelto; el eco trae el nombre en espanol del dia aunque se paso texto ("miercoles").
    [Fact]
    public async Task AsignarTurnoADia_LeeLosDetallesYEnviaElPut_CuandoAmbosExisten()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, DetalleDePlantillaFalso.RutaDetalle(PlantillaIdCocina)),
            (HttpMethod.Get, DetalleDeTurnoFalso.RutaDetalle(TurnoIdCocinaManana)),
            (HttpMethod.Put, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/2/3"));

        var put = fakes.Handler.Requests.Single(r => r.Metodo == HttpMethod.Put);
        JsonNode.Parse(put.Cuerpo!)!["turnoId"]!.GetValue<string>().Should().Be(TurnoIdCocinaManana);

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(AsignarTurnoADiaTool.Mensajes.ResultadoTurnoAsignado);
        json["plantilla"]!.GetValue<string>().Should().Be("Semana Tipo Cocina");
        json["semana"]!.GetValue<int>().Should().Be(2);
        json["dia"]!.GetValue<string>().Should().Be("miercoles");
        json["turno"]!.GetValue<string>().Should().Be("Cocina Manana");
        json["nota"]!.GetValue<string>().Should().Be(AsignarTurnoADiaTool.Mensajes.NotaVisibilidadEventual);
    }

    // Semana omitida -> 1 por defecto; dia recibido como numero "1" y el eco lo devuelve como "lunes".
    [Fact]
    public async Task AsignarTurnoADia_UsaSemanaUnoPorDefectoYEcoConNombreDeDia_CuandoElDiaLlegaComoNumero()
    {
        var fakes = CrearTool(semana: 1, diaIso: 1);

        var resultado = await Ejecutar(
            fakes.Tool, dia: "1", semana: null, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Put && r.Ruta == $"{RutaPlantillas}/{PlantillaIdCocina}/dias/1/1");
        JsonNode.Parse(resultado)!["dia"]!.GetValue<string>().Should().Be("lunes");
        JsonNode.Parse(resultado)!["semana"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task AsignarTurnoADia_RespondePlantillaNoExisteSinPut_Cuando404EnElDetalleDeLaPlantilla()
    {
        var fakes = CrearTool(statusPlantilla: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.PlantillaNoExiste, PlantillaIdCocina));
        resultado.Should().Contain(PlantillaIdCocina).And.Contain("buscar_plantillas_semanales");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    // CA-3: un 404 del detalle del turno remite a buscar_turnos.
    [Fact]
    public async Task AsignarTurnoADia_RespondeTurnoNoExisteSinPut_Cuando404EnElDetalleDelTurno()
    {
        var fakes = CrearTool(statusTurno: HttpStatusCode.NotFound);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.TurnoNoExiste, TurnoIdCocinaManana));
        resultado.Should().Contain(TurnoIdCocinaManana).And.Contain("buscar_turnos");
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoPlantillaIdNoEsUnGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, plantillaId: "Semana Tipo Cocina", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.PlantillaIdInvalido, "Semana Tipo Cocina"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoTurnoIdNoEsUnGuid()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turnoId: "Cocina Manana", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.TurnoIdInvalido, "Cocina Manana"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoDiaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.CampoObligatorio, "dia"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoElDiaEsUnNombreDesconocido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "funes", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.DiaDesconocido, "funes"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoElDiaEsCero()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "0", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.DiaDesconocido, "0"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoElDiaEsOcho()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, dia: "8", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.DiaDesconocido, "8"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoLaSemanaEsMenorAUno()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, semana: 0, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.SemanaInvalida, 0));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_TraduceElRechazoDelDominio_Cuando409PorTurnoIncompleto()
    {
        const string cuerpo = "El turno esta incompleto";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarTurnoADia_TraduceElRechazoDelDominio_Cuando409PorSemanaFueraDeRango()
    {
        const string cuerpo = "La semana esta fuera de rango";
        var fakes = CrearTool(statusPut: HttpStatusCode.Conflict, cuerpoPut: cuerpo);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task AsignarTurnoADia_TraduceElRechazoDelDominio_Cuando400()
    {
        var fixture = Validacion400Json;
        var fakes = CrearTool(statusPut: HttpStatusCode.BadRequest, cuerpoPut: fixture);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, fixture));
    }

    // Boundary del sistema: un 5xx del detalle se traduce a texto y corta antes del PUT (CA-ADR-0030).
    [Fact]
    public async Task AsignarTurnoADia_RespondeElRechazoDelDominioSinPut_CuandoElDetalleDeLaPlantillaFalla()
    {
        var fakes = CrearTool(statusPlantilla: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADia_RespondeElRechazoDelDominioSinPut_CuandoElDetalleDelTurnoFalla()
    {
        var fakes = CrearTool(statusTurno: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    // CA-5: ninguna llamada a los catalogos.
    [Fact]
    public async Task AsignarTurnoADia_NoConsultaLosCatalogos_CuandoAmbosExisten()
    {
        var fakes = CrearTool();

        await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Should().NotContain(r =>
            r.Metodo == HttpMethod.Get && (r.Ruta == RutaPlantillas || r.Ruta == RutaTurnos));
    }
}
