using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.AsignarTurnoADia;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.AsignarTurnoADia;

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
        string? plantillasJson = null,
        HttpStatusCode statusPlantillas = HttpStatusCode.OK,
        string? turnosJson = null,
        HttpStatusCode statusTurnos = HttpStatusCode.OK,
        HttpStatusCode statusPut = HttpStatusCode.NoContent,
        string cuerpoPut = "")
    {
        var (cliente, handler) = ClienteFalso.ConRutas();
        handler.Responde(HttpMethod.Get, RutaPlantillas, statusPlantillas, plantillasJson ?? PlantillasJson);
        handler.Responde(HttpMethod.Get, RutaTurnos, statusTurnos, turnosJson ?? TurnosJson);
        handler.Responde(
            HttpMethod.Put, $"{RutaPlantillas}/{PlantillaIdCocina}/dias/{semana}/{diaIso}", statusPut, cuerpoPut);

        var tool = new AsignarTurnoADiaTool(new ProgramacionApi(cliente));
        return new Fakes(tool, handler);
    }

    private static Task<string> Ejecutar(
        AsignarTurnoADiaTool tool,
        string plantilla = "Semana Tipo Cocina",
        string turno = "Cocina Manana",
        string dia = "miercoles",
        int? semana = 2,
        CancellationToken ct = default) =>
        tool.Run(context: null!, plantilla: plantilla, turno: turno, dia: dia, semana: semana, ct: ct);

    private static void AsegurarNingunaRequest(Fakes fakes) =>
        fakes.Handler.Requests.Should().BeEmpty();

    // CA-1: resuelve plantilla y turno con un GET cada uno, luego el PUT al dia exacto con el
    // turnoId resuelto; el eco trae el nombre en espanol del dia aunque se paso texto ("miercoles").
    [Fact]
    public async Task AsignarTurnoADia_ResuelvePlantillaYTurnoYEnviaElPut_CuandoAmbosExisten()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        fakes.Handler.Requests.Select(r => (r.Metodo, r.Ruta)).Should().Equal(
            (HttpMethod.Get, RutaPlantillas),
            (HttpMethod.Get, RutaTurnos),
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
    public async Task AsignarTurnoADia_RespondePlantillaNoExisteSinPut_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, plantilla: "Plantilla Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            AsignarTurnoADiaTool.Mensajes.PlantillaNoExiste,
            "Plantilla Que No Existe", "Semana Tipo Cocina, Turno Rotativo"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADia_RespondeTurnoNoExisteSinPut_CuandoElNombreNoEstaEnElCatalogo()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(
            fakes.Tool, turno: "Turno Que No Existe", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(
            AsignarTurnoADiaTool.Mensajes.TurnoNoExiste, "Turno Que No Existe", "Cocina Manana, Cocina Tarde"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
        // Plantilla ya se resolvio antes que el turno: el GET de plantillas si ocurre.
        fakes.Handler.Requests.Should().ContainSingle(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaPlantillas);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoPlantillaEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, plantilla: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.CampoObligatorio, "plantilla"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task AsignarTurnoADia_RechazaSinLlamarANingunDominio_CuandoTurnoEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, turno: "   ", ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.CampoObligatorio, "turno"));
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

    // Boundary del sistema: un 5xx del catalogo de plantillas se traduce a texto y corta antes de
    // cualquier otro GET o PUT (CA-ADR-0030).
    [Fact]
    public async Task AsignarTurnoADia_RespondeElRechazoDelDominioSinPut_CuandoElCatalogoDePlantillasFalla()
    {
        var fakes = CrearTool(plantillasJson: "", statusPlantillas: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }

    [Fact]
    public async Task AsignarTurnoADia_RespondeElRechazoDelDominioSinPut_CuandoElCatalogoDeTurnosFalla()
    {
        var fakes = CrearTool(turnosJson: "", statusTurnos: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, ct: TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(AsignarTurnoADiaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Handler.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Put);
    }
}
