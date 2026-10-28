using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantilla;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using static Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte.CicloDeTurnosFalso;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantilla;

public class AplicarPlantillaToolTests
{
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";
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

    private sealed record Entorno(AplicarPlantillaTool Tool, HandlerPorRuta Programacion)
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
        string plantillaJson, string directorioJson, string? sedeDeLaFranja = "SUBA", int diasDeTurnos = 8)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId), HttpStatusCode.OK, plantillaJson);
        programacion.Responde(
            HttpMethod.Get, CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaIdInexistente), HttpStatusCode.NotFound, "");
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, Turnos(sedeDeLaFranja, diasDeTurnos));
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes, (_, _) => EntornoDeSedes.Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.ConFuncion(r => EntornoDeSedes.RespondeSedes(r));
        var (clienteColaboradores, _) = ClienteFalso.Con(directorioJson);

        var tool = new AplicarPlantillaTool(
            new ProgramacionApi(clienteProgramacion), new SedesApi(clienteSedes),
            new ResolutorPlantillaTurnosPorId(new ProgramacionApi(clienteProgramacion)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)),
            new ResolutorCandidatosPorLista(new ColaboradoresApi(clienteColaboradores)));
        return new Entorno(tool, programacion);
    }

    private static Task<string> Ejecutar(
        Entorno entorno, string fechaInicio = "2026-09-01", string desde = "2026-09-01", string hasta = "2026-09-07",
        string plantillaId = PlantillaId, string? sede = null, string identificaciones = "CC-1111") =>
        entorno.Tool.Run(
            null!, plantillaId, fechaInicio, desde, hasta, sede, identificaciones, TestContext.Current.CancellationToken);

    private static void NoHuboPosts(Entorno entorno) =>
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);

    [Fact]
    public async Task AplicarPlantilla_EnviaUnPostPorTurnoDistintoConLasFechasDeSuDiaDelCiclo_CuandoElCicloSeRepite()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        entorno.Posts.Should().HaveCount(3);
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-09-01", "2026-09-04", "2026-09-07");
        Entorno.Fechas(entorno.PostDeTurno(2)).Should().Equal("2026-09-02", "2026-09-05");
        Entorno.Fechas(entorno.PostDeTurno(3)).Should().Equal("2026-09-03", "2026-09-06");
        var json = JsonNode.Parse(resultado)!;
        json["plantilla"]!.GetValue<string>().Should().Be("Ciclo A");
        json["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-01");
        json["programados"]!.AsArray().Single()!["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
    }

    [Fact]
    public async Task AplicarPlantilla_TomaElDiaSeisDelCiclo_CuandoElInicioEsTresDeEneroYLaVentanaEmpiezaElPrimeroDeFebrero()
    {
        var entorno = Crear(Plantilla(dias: 8), DirectorioDe("CC-1111", "2025-01-01"));

        await Ejecutar(entorno, fechaInicio: "2027-01-03", desde: "2027-02-01", hasta: "2027-02-03");

        Entorno.Fechas(entorno.PostDeTurno(6)).Should().Equal("2027-02-01");
        Entorno.Fechas(entorno.PostDeTurno(7)).Should().Equal("2027-02-02");
        Entorno.Fechas(entorno.PostDeTurno(8)).Should().Equal("2027-02-03");
    }

    [Fact]
    public async Task AplicarPlantilla_NoProgramaLosDiasAnterioresAlInicioYLosInforma_CuandoElInicioEsPosteriorADesde()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, fechaInicio: "2026-09-04", desde: "2026-09-01", hasta: "2026-09-07");

        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-09-04", "2026-09-07");
        Entorno.Fechas(entorno.PostDeTurno(2)).Should().Equal("2026-09-05");
        Entorno.Fechas(entorno.PostDeTurno(3)).Should().Equal("2026-09-06");
        var json = JsonNode.Parse(resultado)!;
        json["diasAntesDelInicio"]!.GetValue<int>().Should().Be(3);
        json["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-04");
    }

    [Fact]
    public async Task AplicarPlantilla_OmiteDiasAntesDelInicio_CuandoElInicioNoEsPosteriorADesde()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, fechaInicio: "2026-08-30", desde: "2026-09-01", hasta: "2026-09-03");

        JsonNode.Parse(resultado)!.AsObject().ContainsKey("diasAntesDelInicio").Should().BeFalse();
        Entorno.Fechas(entorno.PostDeTurno(3)).Should().Equal("2026-09-01");
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoElInicioEsPosteriorAHasta()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, fechaInicio: "2026-09-20", desde: "2026-09-01", hasta: "2026-09-14");

        resultado.Should().Be(
            string.Format(AplicarPlantillaTool.Mensajes.FechaInicioPosteriorAHasta, "2026-09-20", "2026-09-14"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_NoReiniciaElCiclo_CuandoLaVigenciaRecortaLosPrimerosDias()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2026-09-03"));

        await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-09-05");

        entorno.Posts.Should().HaveCount(3);
        Entorno.Fechas(entorno.PostDeTurno(3)).Should().Equal("2026-09-03");
        Entorno.Fechas(entorno.PostDeTurno(1)).Should().Equal("2026-09-04");
        Entorno.Fechas(entorno.PostDeTurno(2)).Should().Equal("2026-09-05");
    }

    [Fact]
    public async Task AplicarPlantilla_LeeElDetalleDeLaPlantillaDeTurnos_CuandoLaPlantillaExiste()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        await Ejecutar(entorno);

        entorno.Programacion.Requests.Should().Contain(
            r => r.Metodo == HttpMethod.Get && r.Ruta == CatalogoDePlantillasTurnosFalso.RutaDetalle(PlantillaId));
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaConLaFraseDelId_CuandoLaPlantillaNoExiste()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantillaId: PlantillaIdInexistente);

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.PlantillaNoExiste, PlantillaIdInexistente));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinLlamarAlBackend_CuandoElPlantillaIdNoEsGuid()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, plantillaId: "Ciclo A");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.PlantillaIdInvalido, "Ciclo A"));
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaPlantillaEstaIncompleta()
    {
        var entorno = Crear(Plantilla(completa: false), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.PlantillaIncompleta, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoRetirado()
    {
        var entorno = Crear(Plantilla(diaRetirado: 2), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaPlantillaReferenciaUnTurnoIncompleto()
    {
        var entorno = Crear(Plantilla(diaIncompleto: 2), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno);

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.PlantillaConTurnoNoProgramable, "Ciclo A"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaFechaDeInicioFalta()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, fechaInicio: " ");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.CampoObligatorio, "fecha_inicio"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaFechaDeInicioTieneFormatoInvalido()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, fechaInicio: "03/01/2027");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.FechaInvalida, "fecha_inicio", "03/01/2027"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoDesdeFalta()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.CampoObligatorio, "desde"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoHastaTieneFormatoInvalido()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, hasta: "2026/09/07");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.FechaInvalida, "hasta", "2026/09/07"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaVentanaEstaInvertida()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-09-10", hasta: "2026-09-01");

        resultado.Should().Be(AplicarPlantillaTool.Mensajes.VentanaInvertida);
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaVentanaSupera35Dias()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, desde: "2026-09-01", hasta: "2026-10-06");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.VentanaExcedeMaximo, 36));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_RechazaSinPost_CuandoLaSedeExplicitaNoExiste()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01"));

        var resultado = await Ejecutar(entorno, sede: "FANTASMA");

        resultado.Should().Be(string.Format(AplicarPlantillaTool.Mensajes.SedeNoExiste, "FANTASMA"));
        NoHuboPosts(entorno);
    }

    [Fact]
    public async Task AplicarPlantilla_EnviaLaSedeExplicitaSinLeerElCatalogo_CuandoSeIndicaSedeDeProgramacion()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01", codigoSede: null), sedeDeLaFranja: null);

        var resultado = await Ejecutar(entorno, sede: "SUBA");

        var json = JsonNode.Parse(resultado)!;
        json["sede"]!["codigo"]!.GetValue<string>().Should().Be("SUBA");
        entorno.Posts.Should().HaveCount(3).And.OnlyContain(p => p["sede"]!["id"]!.GetValue<string>() == "SUBA");
        entorno.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Get && r.Ruta == RutaTurnos);
    }

    [Fact]
    public async Task AplicarPlantilla_AvisaSinSede_CuandoElColaboradorNoTieneSedeYUnTurnoTieneFranjaSinSede()
    {
        var entorno = Crear(Plantilla(), DirectorioDe("CC-1111", "2025-01-01", codigoSede: null), sedeDeLaFranja: null);

        var resultado = await Ejecutar(entorno);

        var aviso = JsonNode.Parse(resultado)!["avisos"]!.AsArray().Single()!;
        aviso["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        entorno.Posts.Should().OnlyContain(p => p["sede"] == null);
    }
}
