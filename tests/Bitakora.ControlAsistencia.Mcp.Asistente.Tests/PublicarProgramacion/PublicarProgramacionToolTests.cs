using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.PublicarProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.PublicarProgramacion;

public class PublicarProgramacionToolTests
{
    private const string RutaPublicar = "/api/programacion/colaboradores/AR01/borrador:publicar";

    private static string DirectorioJson => Fixtures.Leer("PublicarProgramacion", "directorio.json");

    private sealed record Fakes(PublicarProgramacionTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Colaboradores);

    private static Fakes CrearTool(
        HttpStatusCode statusPublicar = HttpStatusCode.NoContent,
        string cuerpoPublicar = "",
        HttpStatusCode statusDirectorio = HttpStatusCode.OK,
        string? directorioJson = null)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(HttpMethod.Post, RutaPublicar, statusPublicar, cuerpoPublicar);

        var (clienteColaboradores, handlerColaboradores) =
            ClienteFalso.Con(directorioJson ?? DirectorioJson, statusDirectorio);
        var tool = new PublicarProgramacionTool(
            new ProgramacionApi(clienteProgramacion), new ColaboradoresApi(clienteColaboradores));
        return new Fakes(tool, handlerProgramacion, handlerColaboradores);
    }

    private static Task<string> Ejecutar(
        PublicarProgramacionTool tool,
        string identificacion = "CC-1111",
        string desde = "2026-09-01",
        string hasta = "2026-09-03") =>
        tool.Run(
            context: null!,
            identificacion: identificacion,
            desde: desde,
            hasta: hasta,
            ct: TestContext.Current.CancellationToken);

    [Fact]
    public async Task PublicarProgramacion_LlamaUnaVezAlActoYResponde_CuandoElColaboradorYElPeriodoSonValidos()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool);

        var llamada = fakes.Programacion.Requests.Should().ContainSingle().Subject;
        llamada.Metodo.Should().Be(HttpMethod.Post);
        llamada.Ruta.Should().Be(RutaPublicar);
        var cuerpo = JsonNode.Parse(llamada.Cuerpo!)!;
        cuerpo["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        cuerpo["hasta"]!.GetValue<string>().Should().Be("2026-09-03");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(PublicarProgramacionTool.Mensajes.ResultadoProgramacionPublicada);
        json["colaborador"]!.GetValue<string>().Should().Be("Ana Ruiz");
        json["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-09-03");
    }

    [Fact]
    public async Task PublicarProgramacion_RespondeSoloElEcoDelActo_CuandoPublica()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool);

        JsonNode.Parse(resultado)!.AsObject().Select(p => p.Key)
            .Should().BeEquivalentTo(["resultado", "colaborador", "desde", "hasta"]);
    }

    [Fact]
    public async Task PublicarProgramacion_AceptaElPeriodo_CuandoDura366Dias()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-01-01", hasta: "2027-01-01");

        fakes.Programacion.Requests.Should().ContainSingle();
        JsonNode.Parse(resultado)!["resultado"]!.GetValue<string>()
            .Should().Be(PublicarProgramacionTool.Mensajes.ResultadoProgramacionPublicada);
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoElPeriodoDura367Dias()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-01-01", hasta: "2027-01-02");

        resultado.Should().Be(PublicarProgramacionTool.Mensajes.PeriodoExcedeElMaximo);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoFaltaIdentificacion()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: " ");

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.CampoObligatorio, "identificacion"));
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Colaboradores.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoFaltaDesde()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "");

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.CampoObligatorio, "desde"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoFaltaHasta()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, hasta: "");

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.CampoObligatorio, "hasta"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoLaFechaTieneFormatoInvalido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, hasta: "03-09-2026");

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.FechaInvalida, "hasta", "03-09-2026"));
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Colaboradores.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-30", hasta: "2026-09-20");

        resultado.Should().Be(PublicarProgramacionTool.Mensajes.PeriodoInvertido);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_RechazaSinEscribir_CuandoLaIdentificacionNoEstaEnElDirectorio()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-9999");

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.ColaboradorNoEncontrado, "CC-9999"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_DevuelveElRechazoComoTexto_CuandoElDirectorioRechaza()
    {
        const string rechazo = "Directorio no disponible";
        var fakes = CrearTool(statusDirectorio: HttpStatusCode.ServiceUnavailable, directorioJson: rechazo);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.RechazoDelDominio, rechazo));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicarProgramacion_DevuelveElRechazoComoTexto_CuandoElActoRechaza()
    {
        const string rechazo = "Rango invalido";
        var fakes = CrearTool(HttpStatusCode.BadRequest, rechazo);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(PublicarProgramacionTool.Mensajes.RechazoDelDominio, rechazo));
    }
}
