using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CicloDeTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaPorGrupo;

public class AplicarPlantillaPorGrupoToolTests
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

    private sealed record Entorno(
        AplicarPlantillaPorGrupoTool Tool, HandlerPorRuta Programacion, List<JsonNode> ConsultasDeFichas)
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

    private static Entorno Crear(IReadOnlyList<object>? fichas = null, string? plantillaJson = null)
    {
        var consultas = new List<JsonNode>();
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId), HttpStatusCode.OK,
            plantillaJson ?? Plantilla());
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaIdInexistente), HttpStatusCode.NotFound, "");
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos("SUBA"));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes, (_, _) => EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.ConFuncion(request =>
        {
            var cuerpo = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            lock (consultas)
                consultas.Add(JsonNode.Parse(cuerpo)!);
            return EntornoDeSedes.Respuesta(HttpStatusCode.OK, JsonSerializer.Serialize(new { elementos = fichas ?? [] }, Web));
        });

        var tool = new AplicarPlantillaPorGrupoTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ResolutorPlantillaTurnosPorId(new ProgramacionApi(clienteProgramacion)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteColaboradores)));
        return new Entorno(tool, programacion, consultas);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string fechaInicio = "2026-09-01", string desde = "2026-09-01", string hasta = "2026-09-07",
        string plantillaId = PlantillaId, string? sedeDeProgramacion = null, string? sede = null,
        string? etiquetas = "area:cocina") =>
        entorno.Tool.Run(
            null!, plantillaId, fechaInicio, desde, hasta, sedeDeProgramacion, sede, etiquetas,
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AplicarPlantillaPorGrupo_ProgramaATodoElGrupoElTurnoDelDiaDelCiclo_CuandoElCicloSeRepite()
    {
        var entorno = Crear(fichas: [Ficha(1), Ficha(2)]);

        var resultado = await Ejecutar(entorno);

        entorno.Posts.Should().HaveCount(3);
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-09-01", "2026-09-04", "2026-09-07");
        entorno.PostDeTurno(1)["colaboradores"]!.AsArray().Should().HaveCount(2);
        var json = JsonNode.Parse(resultado)!;
        json["grupoResuelto"]!.GetValue<int>().Should().Be(2);
        json["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-01");
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_TomaElDiaSeisDelCiclo_CuandoElInicioEsTresDeEneroYLaVentanaEmpiezaElPrimeroDeFebrero()
    {
        var entorno = Crear(fichas: [Ficha(1)], plantillaJson: Plantilla(dias: 8));

        await Ejecutar(entorno, fechaInicio: "2027-01-03", desde: "2027-02-01", hasta: "2027-02-02");

        Entorno.Fechas(entorno.PostDeTurno(6)).Should().Equal("2027-02-01");
        Entorno.Fechas(entorno.PostDeTurno(7)).Should().Equal("2027-02-02");
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_NoProgramaLosDiasAnterioresAlInicioYLosInforma_CuandoElInicioEsPosteriorADesde()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, fechaInicio: "2026-09-04");

        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-09-04", "2026-09-07");
        JsonNode.Parse(resultado)!["diasAntesDelInicio"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoElInicioEsPosteriorAHasta()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, fechaInicio: "2026-09-20");

        resultado.Should().Be(
            string.Format(AplicarPlantillaPorGrupoTool.Mensajes.FechaInicioPosteriorAHasta, "2026-09-20", "2026-09-07"));
        entorno.Posts.Should().BeEmpty();
        entorno.ConsultasDeFichas.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoLaFechaDeInicioTieneFormatoInvalido()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, fechaInicio: "03/01/2027");

        resultado.Should().Be(
            string.Format(AplicarPlantillaPorGrupoTool.Mensajes.FechaInvalida, "fecha_inicio", "03/01/2027"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoLaFechaDeInicioFalta()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, fechaInicio: "");

        resultado.Should().Be(string.Format(AplicarPlantillaPorGrupoTool.Mensajes.CampoObligatorio, "fecha_inicio"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoNoLlegaSedeNiEtiquetas()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, sede: null, etiquetas: null);

        resultado.Should().Be(AplicarPlantillaPorGrupoTool.Mensajes.SelectorObligatorio);
        entorno.Posts.Should().BeEmpty();
        entorno.ConsultasDeFichas.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaConLaFraseDelId_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, plantillaId: PlantillaIdInexistente);

        resultado.Should().Be(
            string.Format(AplicarPlantillaPorGrupoTool.Mensajes.PlantillaNoExiste, PlantillaIdInexistente));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinLlamarAlBackend_CuandoElPlantillaIdNoEsGuid()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, plantillaId: "Ciclo A");

        resultado.Should().Be(string.Format(AplicarPlantillaPorGrupoTool.Mensajes.PlantillaIdInvalido, "Ciclo A"));
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(fichas: [Ficha(1)], plantillaJson: Plantilla(diaRetirado: 2));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(
            string.Format(AplicarPlantillaPorGrupoTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_RechazaSinEscribir_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-10-06");

        resultado.Should().Be(string.Format(AplicarPlantillaPorGrupoTool.Mensajes.VentanaExcedeMaximo, 36));
        entorno.Posts.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantillaPorGrupo_EnviaLaSedeExplicita_CuandoSeIndicaSedeDeProgramacion()
    {
        var entorno = Crear(fichas: [Ficha(1)]);

        var resultado = await Ejecutar(entorno, sedeDeProgramacion: "SUBA");

        JsonNode.Parse(resultado)!["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        entorno.Posts.Should().HaveCount(3).And.OnlyContain(p => p["sede"]!["id"]!.GetValue<string>() == "SUBA");
    }
}
