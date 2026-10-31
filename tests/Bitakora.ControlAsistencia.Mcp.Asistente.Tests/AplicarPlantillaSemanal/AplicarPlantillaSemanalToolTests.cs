using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CicloDeTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaSemanal;

public class AplicarPlantillaSemanalToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes-en-fechas";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string DirectorioDe(string identificacion, string vigenteDesde, string? codigoSede = "SUBA") =>
        JsonSerializer.Serialize(
            new
            {
                elementos = new[]
                {
                    new
                    {
                        identificacion, nombreCompleto = "Nombre AR01", codigoColaborador = "AR01", codigoSede,
                        vigenteDesde, vigenteHasta = (string?)null
                    }
                }
            },
            Web);

    private sealed record Entorno(AplicarPlantillaSemanalTool Tool, HandlerPorRuta Programacion)
    {
        public List<JsonNode> Posts =>
            [.. Programacion.Requests
                .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
                .Select(r => JsonNode.Parse(r.Cuerpo!)!)];

        public JsonNode PostDeTurno(int dia) =>
            Posts.Single(p => p["turnoId"]!.GetValue<string>() == TurnoDelDia(dia));

        public static string[] Fechas(JsonNode post) =>
            [.. post["fechas"]!.AsArray().Select(f => f!.GetValue<string>())];
    }

    private static Entorno Crear(
        string plantillaJson, string directorioJson, string? sedeDeLaFranja = "SUBA",
        Func<string?, HttpResponseMessage>? respuestaPost = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId), HttpStatusCode.OK, plantillaJson);
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaIdInexistente), HttpStatusCode.NotFound, "");
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos(sedeDeLaFranja));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.Con(directorioJson);

        var tool = new AplicarPlantillaSemanalTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ResolutorPlantillaTurnosPorId(new ProgramacionApi(clienteProgramacion)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorLista(new ColaboradoresApi(clienteColaboradores)));
        return new Entorno(tool, programacion);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string desde = "2026-11-02", string hasta = "2026-11-08",
        string plantillaId = PlantillaId, string? sede = null, string identificaciones = "CC-1111") =>
        entorno.Tool.Run(
            null!, desde, hasta, plantillaId, sede, identificaciones, TestContext.Current.CancellationToken);

    private static void NoHuboPosts(Entorno entorno) =>
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);

    [Fact]
    public async Task AplicarPlantillaSemanal_TomaElDiaTresDelCiclo_CuandoDesdeEsMiercoles()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-11-04", hasta: "2026-11-10");

        Entorno.Fechas(entorno.PostDeTurno(3)).Should().Equal("2026-11-04");
        Entorno.Fechas(entorno.PostDeTurno(4)).Should().Equal("2026-11-05");
        Entorno.Fechas(entorno.PostDeTurno(7)).Should().Equal("2026-11-08");
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-11-09");
        Entorno.Fechas(entorno.PostDeTurno(2)).Should().Equal("2026-11-10");
        JsonNode.Parse(resultado)!["plantilla"]!.GetValue<string>().Should().Be("Ciclo A");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_TomaElDiaUnoElLunes_CuandoDesdeEsLunes()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        await Ejecutar(entorno, desde: "2026-11-02", hasta: "2026-11-08");

        entorno.Posts.Should().HaveCount(7);
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-11-02");
        Entorno.Fechas(entorno.PostDeTurno(7)).Should().Equal("2026-11-08");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_TomaElDiaSieteElDomingo_CuandoDesdeEsDomingo()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        await Ejecutar(entorno, desde: "2026-11-08", hasta: "2026-11-09");

        Entorno.Fechas(entorno.PostDeTurno(7)).Should().Equal("2026-11-08");
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-11-09");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaRemitiendoAAplicarPlantilla_CuandoElCicloNoEsDeSieteDias()
    {
        var entorno = Crear(Plantilla(dias: 14), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaNoEsSemanal, "Ciclo A", 14));
        resultado.Should().Contain("aplicar_plantilla");
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoElCicloEsDeTresDias()
    {
        var entorno = Crear(Plantilla(dias: 3), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaNoEsSemanal, "Ciclo A", 3));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_LeeElDetalleDeLaPlantillaDeTurnos_CuandoLaPlantillaExiste()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        await Ejecutar(entorno);

        entorno.Programacion.Requests.Should().Contain(
            r => r.Metodo == HttpMethod.Get && r.Ruta == CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId));
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaConLaFraseDelId_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantillaId: PlantillaIdInexistente);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaNoExiste, PlantillaIdInexistente));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinLlamarAlBackend_CuandoElPlantillaIdNoEsGuid()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantillaId: "Semana A");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaIdInvalido, "Semana A"));
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaEstaIncompleta()
    {
        var entorno = Crear(Plantilla(dias: 7, completa: false), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaIncompleta, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(Plantilla(dias: 7, diaRetirado: 5), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoIncompleto()
    {
        var entorno = Crear(Plantilla(dias: 7, diaIncompleto: 2), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-11-02", hasta: "2026-12-07");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.VentanaExcedeMaximo, 36));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_ReportaFallidoConElTurnoYProgramaElResto_CuandoUnPostEsRechazado()
    {
        var entorno = Crear(
            Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01"),
            respuestaPost: cuerpo => cuerpo!.Contains(TurnoDelDia(2))
                ? EntornoDeSedes.Respuesta(HttpStatusCode.Conflict, "turno rechazado")
                : EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));

        var resultado = await Ejecutar(entorno);

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().ContainSingle();
        json["fallidos"]!.AsArray().Single()!["turno"]!.GetValue<string>().Should().Be("Turno 2");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_EnviaLaSedeExplicitaSinLeerElCatalogo_CuandoSeIndicaSedeDeProgramacion()
    {
        var entorno = Crear(Plantilla(dias: 7), DirectorioDe("CC-1111", "2025-01-01", codigoSede: null), sedeDeLaFranja: null);

        var resultado = await Ejecutar(entorno, sede: "SUBA");

        JsonNode.Parse(resultado)!["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        entorno.Posts.Should().HaveCount(7).And.OnlyContain(p => p["sede"]!["id"]!.GetValue<string>() == "SUBA");
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurnos);
    }
}
