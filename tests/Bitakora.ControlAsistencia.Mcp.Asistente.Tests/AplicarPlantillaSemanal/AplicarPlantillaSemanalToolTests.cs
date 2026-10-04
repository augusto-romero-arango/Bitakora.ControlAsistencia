using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaSemanal;

public class AplicarPlantillaSemanalToolTests
{
    private const string RutaPlantillas = "/api/programacion/plantillas-semanales";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";
    private const string TurnoA = "8f14e45f-ceea-4b3c-8f0a-000000000001";
    private const string TurnoB = "8f14e45f-ceea-4b3c-8f0a-000000000002";
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static object Slot(int semana, int dia, string id, bool completo = true, bool retirado = false) => new
    {
        semana, dia, turno = new { id, nombre = $"Turno {id[^1]}", descripcion = "(06:00-14:00)", completo, retirado }
    };

    private static string Plantillas(
        string nombre = "Semana A", int semanas = 2, bool completa = true,
        bool turnoBCompleto = true, bool turnoBRetirado = false) => JsonSerializer.Serialize(
        new[]
        {
            new
            {
                id = "01a07000-1000-7000-9000-000000000001",
                nombre,
                semanas,
                completa,
                dias = new[]
                {
                    Slot(1, 1, TurnoA), Slot(1, 2, TurnoA), Slot(1, 3, TurnoA), Slot(1, 4, TurnoA),
                    Slot(1, 5, TurnoA), Slot(1, 6, TurnoA), Slot(1, 7, TurnoA),
                    Slot(2, 1, TurnoB, turnoBCompleto, turnoBRetirado), Slot(2, 2, TurnoB, turnoBCompleto, turnoBRetirado),
                    Slot(2, 3, TurnoB, turnoBCompleto, turnoBRetirado), Slot(2, 4, TurnoB, turnoBCompleto, turnoBRetirado),
                    Slot(2, 5, TurnoB, turnoBCompleto, turnoBRetirado), Slot(2, 6, TurnoB, turnoBCompleto, turnoBRetirado),
                    Slot(2, 7, TurnoB, turnoBCompleto, turnoBRetirado)
                }
            },
            new
            {
                id = "01a07000-1000-7000-9000-000000000002", nombre = "Semana B", semanas = 1, completa = true,
                dias = Array.Empty<object>()
            }
        },
        Web);

    private static string Turnos(string? sedeIdDeLaFranja) => JsonSerializer.Serialize(
        new[] { TurnoA, TurnoB }.Select(id => new
        {
            id,
            nombre = $"Turno {id[^1]}",
            esDescanso = false,
            horarioResumido = "06:00-14:00",
            franjas = new[]
            {
                new
                {
                    horaInicio = "06:00:00", horaFin = "14:00:00", diaOffsetFin = 0,
                    descansos = Array.Empty<object>(), extras = Array.Empty<object>(),
                    sedeId = sedeIdDeLaFranja, nombreSede = (string?)null, descripcion = ""
                }
            },
            descripcion = "",
            completo = true
        }),
        Web);

    private static string DirectorioDe(string identificacion, string vigenteDesde, string? codigoSede = "SUBA") =>
        JsonSerializer.Serialize(
            new[]
            {
                new
                {
                    identificacion, nombreCompleto = "Nombre AR01", codigoColaborador = "AR01", codigoSede,
                    vigenteDesde, vigenteHasta = (string?)null
                }
            },
            Web);

    private sealed record Entorno(AplicarPlantillaSemanalTool Tool, HandlerPorRuta Programacion)
    {
        public List<JsonNode> Posts =>
            [.. Programacion.Requests
                .Where(r => r.Metodo == HttpMethod.Post && r.Ruta == RutaSolicitudes)
                .Select(r => JsonNode.Parse(r.Cuerpo!)!)];

        public JsonNode PostDeTurno(string turnoId) =>
            Posts.Single(p => p["turnoId"]!.GetValue<string>() == turnoId);

        public static string[] Fechas(JsonNode post) =>
            [.. post["fechas"]!.AsArray().Select(f => f!.GetValue<string>())];
    }

    private static Entorno Crear(
        string plantillasJson, string directorioJson, string? sedeDeLaFranja = "SUBA",
        Func<string?, HttpResponseMessage>? respuestaPost = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaPlantillas, HttpStatusCode.OK, plantillasJson);
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos(sedeDeLaFranja));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.Con(directorioJson);

        var tool = new AplicarPlantillaSemanalTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores));
        return new Entorno(tool, programacion);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string desde = "2026-09-01", string hasta = "2026-09-14",
        string plantilla = "Semana A", string? sede = null, string identificaciones = "CC-1111") =>
        entorno.Tool.Run(
            null!, desde, hasta, plantilla, sede, identificaciones, TestContext.Current.CancellationToken);

    private static void NoHuboPosts(Entorno entorno) =>
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaListandoLosNombres_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantilla: "Inexistente");

        resultado.Should().Contain("Inexistente").And.Contain("Semana A").And.Contain("Semana B");
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaEstaIncompleta()
    {
        var entorno = Crear(Plantillas(completa: false), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaIncompleta, "Semana A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(Plantillas(turnoBRetirado: true), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(
            string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaConTurnoNoProgramable, "Semana A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoIncompleto()
    {
        var entorno = Crear(Plantillas(turnoBCompleto: false), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(
            string.Format(AplicarPlantillaSemanalTool.Mensajes.PlantillaConTurnoNoProgramable, "Semana A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-10-06");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.VentanaExcedeMaximo, 36));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_RechazaSinPost_CuandoLaFechaTieneFormatoInvalido()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "01/09/2026");

        resultado.Should().Be(string.Format(AplicarPlantillaSemanalTool.Mensajes.FechaInvalida, "desde", "01/09/2026"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_ResuelveElNombreNormalizado_CuandoDifierenEspaciosYMayusculas()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantilla: "  semana   a ");

        JsonNode.Parse(resultado)!["programados"]!.AsArray().Should().ContainSingle();
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_EnviaUnPostPorTurnoDistintoConSusFechas_CuandoElMoldeAlternaSemanas()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-09-02", hasta: "2026-09-14");

        entorno.Posts.Should().HaveCount(2);
        Entorno.Fechas(entorno.PostDeTurno(TurnoA)).Should().Equal(
            "2026-09-02", "2026-09-03", "2026-09-04", "2026-09-05", "2026-09-06", "2026-09-14");
        Entorno.Fechas(entorno.PostDeTurno(TurnoB)).Should().Equal(
            "2026-09-07", "2026-09-08", "2026-09-09", "2026-09-10", "2026-09-11", "2026-09-12", "2026-09-13");
        var json = JsonNode.Parse(resultado)!;
        json["plantilla"]!.GetValue<string>().Should().Be("Semana A");
        json["sede"].Should().BeNull();
        json["programados"]!.AsArray().Single()!["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_NoReiniciaElMolde_CuandoLaVigenciaRecortaLosPrimerosDias()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2026-09-09"));

        await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-09-14");

        entorno.Posts.Should().HaveCount(2);
        Entorno.Fechas(entorno.PostDeTurno(TurnoB)).Should().Equal(
            "2026-09-09", "2026-09-10", "2026-09-11", "2026-09-12", "2026-09-13");
        Entorno.Fechas(entorno.PostDeTurno(TurnoA)).Should().Equal("2026-09-14");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_ReportaFallidoConElTurnoYProgramaElResto_CuandoUnPostEsRechazado()
    {
        var entorno = Crear(
            Plantillas(), DirectorioDe("CC-1111", "2025-01-01"),
            respuestaPost: cuerpo => cuerpo!.Contains(TurnoB)
                ? EntornoDeSedes.Respuesta(HttpStatusCode.Conflict, "turno rechazado")
                : EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));

        var resultado = await Ejecutar(entorno);

        var json = JsonNode.Parse(resultado)!;
        json["programados"]!.AsArray().Should().ContainSingle();
        var fallido = json["fallidos"]!.AsArray().Single()!;
        fallido["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        fallido["turno"]!.GetValue<string>().Should().Be("Turno 2");
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_AvisaSinSede_CuandoElColaboradorNoTieneSedeYUnTurnoTieneFranjaSinSede()
    {
        var entorno = Crear(Plantillas(), DirectorioDe("CC-1111", "2025-01-01", codigoSede: null), sedeDeLaFranja: null);

        var resultado = await Ejecutar(entorno);

        var aviso = JsonNode.Parse(resultado)!["avisos"]!.AsArray().Single()!;
        aviso["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        entorno.Posts.Should().OnlyContain(p => p["sede"] == null);
        entorno.Programacion.Requests.Count(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurnos)
            .Should().Be(1);
    }

    [Fact]
    public async Task AplicarPlantillaSemanal_NoAvisa_CuandoTodasLasFranjasTienenSede()
    {
        var entorno = Crear(
            Plantillas(), DirectorioDe("CC-1111", "2025-01-01", codigoSede: null), sedeDeLaFranja: "SUBA");

        var resultado = await Ejecutar(entorno);

        JsonNode.Parse(resultado)!.AsObject().ContainsKey("avisos").Should().BeFalse();
    }
}
