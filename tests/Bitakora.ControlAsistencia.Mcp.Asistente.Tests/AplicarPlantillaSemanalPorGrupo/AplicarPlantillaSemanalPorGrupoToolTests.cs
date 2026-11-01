using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanalPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CicloDeTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaSemanalPorGrupo;

public class AplicarPlantillaSemanalPorGrupoToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static object Ficha(int numero, string vigenteDesde = "2025-01-01") => new
    {
        id = $"CC-{numero:D4}",
        nombreCompleto = $"Colab {numero:D4}",
        codigoColaborador = $"C{numero:D4}",
        vigenteDesde,
        vigenteHasta = (string?)null,
        etiquetas = Array.Empty<object>(),
        codigoSede = "SUBA"
    };

    private sealed record Entorno(AplicarPlantillaSemanalPorGrupoTool Tool, HandlerPorRuta Programacion)
    {
        public List<JsonNode> Posts =>
            [.. Programacion.Requests
                .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
                .Select(r => JsonNode.Parse(r.Cuerpo!)!)];

        public List<JsonNode> PostsDeTurno(int dia) =>
            [.. Posts.Where(p => p["turnoId"]!.GetValue<string>() == TurnoDelDia(dia))];

        public static string[] Fechas(JsonNode post) =>
            [.. post["fechas"]!.AsArray().Select(f => f!.GetValue<string>())];
    }

    private static Entorno Crear(IReadOnlyList<object>? fichas = null, string? plantillaJson = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId), HttpStatusCode.OK,
            plantillaJson ?? Plantilla(dias: 7));
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaIdInexistente), HttpStatusCode.NotFound, "");
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos("SUBA"));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes, (_, _) => EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.ConFuncion(_ =>
            EntornoDeSedes.Respuesta(HttpStatusCode.OK, JsonSerializer.Serialize(new { elementos = fichas ?? [] }, Web)));

        var tool = new AplicarPlantillaSemanalPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ResolutorPlantillaTurnosPorId(new ProgramacionApi(clienteProgramacion)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteColaboradores)));
        return new Entorno(tool, programacion);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string desde = "2026-11-02", string hasta = "2026-11-08",
        string plantillaId = PlantillaId, string? sedeDeProgramacion = null, string? sede = null,
        string? etiquetas = "area:cocina") =>
        entorno.Tool.Run(
            null!, desde, hasta, plantillaId, sedeDeProgramacion, sede, etiquetas,
            TestContext.Current.CancellationToken);

    private static void NoHuboPosts(Entorno entorno) =>
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_TomaElDiaTresDelCicloParaTodoElGrupo_CuandoDesdeEsMiercoles()
    {
        var entorno = Crear(fichas: [Ficha(1), Ficha(2)]);

        var resultado = await Ejecutar(entorno, desde: "2026-11-04", hasta: "2026-11-10");

        var postsDelDiaTres = entorno.PostsDeTurno(3);
        postsDelDiaTres.Select(p => p["colaborador"]!["identificacion"]!.GetValue<string>())
            .Should().BeEquivalentTo("CC-0001", "CC-0002");
        postsDelDiaTres.Should().AllSatisfy(p => Entorno.Fechas(p).Should().Equal("2026-11-04"));
        entorno.PostsDeTurno(1).Should().AllSatisfy(p => Entorno.Fechas(p).Should().Equal("2026-11-09"));
        var json = JsonNode.Parse(resultado)!;
        json["grupoResuelto"]!.GetValue<int>().Should().Be(2);
        json["plantilla"]!.GetValue<string>().Should().Be("Ciclo A");
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaRemitiendoAAplicarPlantillaPorGrupo_CuandoElCicloNoEsDeSieteDias()
    {
        var entorno = Crear(fichas: [Ficha(1)], plantillaJson: Plantilla(dias: 14));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(
            string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaNoEsSemanal, "Ciclo A", 14));
        resultado.Should().Contain("aplicar_plantilla_por_grupo");
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, plantillaId: PlantillaIdInexistente);

        resultado.Should().Be(
            string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaNoExiste, PlantillaIdInexistente));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinLlamarAlBackend_CuandoElPlantillaIdNoEsGuid()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, plantillaId: "Semana A");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaIdInvalido, "Semana A"));
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaEstaIncompleta()
    {
        var entorno = Crear(fichas: [Ficha(1)], plantillaJson: Plantilla(dias: 7, completa: false));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaIncompleta, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(fichas: [Ficha(1)], plantillaJson: Plantilla(dias: 7, diaRetirado: 4));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(
            string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_RechazaSinEscribir_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, desde: "2026-11-02", hasta: "2026-12-07");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalPorGrupoTool.Mensajes.VentanaExcedeMaximo, 36));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanalPorGrupo_ReportaCeroProgramados_CuandoElGrupoEstaVacio()
    {
        var entorno = Crear(fichas: []);

        var resultado = await Ejecutar(entorno);

        JsonNode.Parse(resultado)!["programados"]!.AsArray().Should().BeEmpty();
        NoHuboPosts(entorno);
    }
}
