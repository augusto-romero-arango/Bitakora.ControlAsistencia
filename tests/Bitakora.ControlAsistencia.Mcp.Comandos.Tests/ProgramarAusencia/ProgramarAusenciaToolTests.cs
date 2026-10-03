using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.ProgramarAusencia;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.ProgramarAusencia;

public class ProgramarAusenciaToolTests
{
    private const string RutaAusenciasAna = "/api/programacion/colaboradores/AR01/ausencias";
    private const string RutaAusenciasBeto = "/api/programacion/colaboradores/BD01/ausencias";
    private const string RutaAusenciasDario = "/api/programacion/colaboradores/DM01/ausencias";

    private static string DirectorioJson => Fixtures.Leer("ProgramarAusencia", "directorio.json");

    private sealed record Fakes(ProgramarAusenciaTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Colaboradores);

    private static Fakes CrearTool(
        HttpStatusCode statusAusencia = HttpStatusCode.Created,
        string cuerpoAusencia = "",
        HttpStatusCode statusDirectorio = HttpStatusCode.OK,
        string? directorioJson = null)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        foreach (var ruta in new[] { RutaAusenciasAna, RutaAusenciasBeto, RutaAusenciasDario })
            handlerProgramacion.Responde(HttpMethod.Post, ruta, statusAusencia, cuerpoAusencia);

        var (clienteColaboradores, handlerColaboradores) = ClienteFalso.Con(
            directorioJson ?? DirectorioJson, statusDirectorio);

        var tool = new ProgramarAusenciaTool(
            new ProgramacionApi(clienteProgramacion), new ColaboradoresApi(clienteColaboradores));
        return new Fakes(tool, handlerProgramacion, handlerColaboradores);
    }

    private static Task<string> Ejecutar(
        ProgramarAusenciaTool tool,
        string identificacion = "CC-1111",
        string desde = "2026-09-13",
        string hasta = "2026-09-26",
        string motivo = "IncapacidadMedica") =>
        tool.Run(
            context: null!,
            identificacion: identificacion,
            desde: desde,
            hasta: hasta,
            motivo: motivo,
            ct: TestContext.Current.CancellationToken);

    private static void AsegurarNingunaRequest(Fakes fakes)
    {
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Colaboradores.UltimaRequest.Should().BeNull();
    }

    // CA-1
    [Fact]
    public async Task ProgramarAusencia_RegistraElPeriodoCompleto_CuandoLaVinculacionLoCubre()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool);

        fakes.Colaboradores.UltimaRequest!.Method.Method.Should().Be("QUERY");
        var posts = fakes.Programacion.Requests.Where(r => r.Metodo == HttpMethod.Post).ToList();
        posts.Should().ContainSingle();
        posts[0].Ruta.Should().Be(RutaAusenciasAna);
        var cuerpo = JsonNode.Parse(posts[0].Cuerpo!)!;
        var id = Guid.Parse(cuerpo["id"]!.GetValue<string>());
        id.Version.Should().Be(7);
        cuerpo["identificacion"]!.GetValue<string>().Should().Be("CC-1111");
        cuerpo["nombreCompleto"]!.GetValue<string>().Should().Be("Ana Ruiz");
        cuerpo["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-13");
        cuerpo["fechaFin"]!.GetValue<string>().Should().Be("2026-09-26");
        cuerpo["motivo"]!.GetValue<string>().Should().Be("IncapacidadMedica");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(ProgramarAusenciaTool.Mensajes.ResultadoAusenciaRegistrada);
        json["colaborador"]!.GetValue<string>().Should().Be("Ana Ruiz");
        json["motivo"]!.GetValue<string>().Should().Be("IncapacidadMedica");
        json["desde"]!.GetValue<string>().Should().Be("2026-09-13");
        json["hasta"]!.GetValue<string>().Should().Be("2026-09-26");
        json.AsObject().ContainsKey("diasFuera").Should().BeFalse("la vinculacion cubre todo el periodo");
    }

    // CA-2: el periodo pasa el fin de la vinculacion (Beto termina el 2026-09-20).
    [Fact]
    public async Task ProgramarAusencia_RecortaAlFinDeLaVinculacion_CuandoElPeriodoLaSupera()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-2222", desde: "2026-09-15", hasta: "2026-09-25");

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        post.Ruta.Should().Be(RutaAusenciasBeto);
        var cuerpo = JsonNode.Parse(post.Cuerpo!)!;
        cuerpo["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-15");
        cuerpo["fechaFin"]!.GetValue<string>().Should().Be("2026-09-20");

        var json = JsonNode.Parse(resultado)!;
        json["hasta"]!.GetValue<string>().Should().Be("2026-09-20");
        json["diasFuera"]!.AsArray().Select(d => d!.GetValue<string>()).Should().Equal(
            "2026-09-21", "2026-09-22", "2026-09-23", "2026-09-24", "2026-09-25");
    }

    // CA-2: el periodo empieza antes del inicio de la vinculacion (Dario empieza el 2026-09-10).
    [Fact]
    public async Task ProgramarAusencia_RecortaAlInicioDeLaVinculacion_CuandoElPeriodoEmpiezaAntes()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-4444", desde: "2026-09-05", hasta: "2026-09-14");

        var post = fakes.Programacion.Requests.Single(r => r.Metodo == HttpMethod.Post);
        post.Ruta.Should().Be(RutaAusenciasDario);
        var cuerpo = JsonNode.Parse(post.Cuerpo!)!;
        cuerpo["fechaInicio"]!.GetValue<string>().Should().Be("2026-09-10");
        cuerpo["fechaFin"]!.GetValue<string>().Should().Be("2026-09-14");

        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-09-10");
        json["diasFuera"]!.AsArray().Select(d => d!.GetValue<string>()).Should().Equal(
            "2026-09-05", "2026-09-06", "2026-09-07", "2026-09-08", "2026-09-09");
    }

    // CA-2: Caro termino su vinculacion el 2026-08-30; ningun dia del periodo cae en ella.
    [Fact]
    public async Task ProgramarAusencia_ExplicaYNoRegistra_CuandoNingunDiaCaeEnLaVinculacion()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-3333", desde: "2026-09-01", hasta: "2026-09-10");

        resultado.Should().Be(string.Format(
            ProgramarAusenciaTool.Mensajes.SinDiasVinculados, "Caro Leon", "2026-09-01 a 2026-09-10"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    // CA-3
    [Fact]
    public async Task ProgramarAusencia_RechazaSinEscribir_CuandoElColaboradorNoEstaEnElDirectorio()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-9999");

        resultado.Should().Be(string.Format(ProgramarAusenciaTool.Mensajes.ColaboradorNoEncontrado, "CC-9999"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ProgramarAusencia_RechazaSinEscribir_CuandoElMotivoNoEstaEnLaLista()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, motivo: "Descanso");

        resultado.Should().Contain("Descanso")
            .And.Contain("Vacaciones").And.Contain("IncapacidadMedica")
            .And.Contain("LicenciaRemunerada").And.Contain("AusenciaNoRemunerada");
        resultado.Should().StartWith(string.Format(ProgramarAusenciaTool.Mensajes.MotivoInvalido, "Descanso", "").Split("Motivos")[0]);
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task ProgramarAusencia_RechazaSinEscribir_CuandoLaFechaTieneFormatoInvalido()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "13/09/2026");

        resultado.Should().Be(string.Format(ProgramarAusenciaTool.Mensajes.FechaInvalida, "desde", "13/09/2026"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task ProgramarAusencia_RechazaSinEscribir_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-26", hasta: "2026-09-13");

        resultado.Should().Be(ProgramarAusenciaTool.Mensajes.PeriodoInvertido);
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task ProgramarAusencia_RechazaSinEscribir_CuandoLaIdentificacionEstaEnBlanco()
    {
        var fakes = CrearTool();

        var resultado = await Ejecutar(fakes.Tool, identificacion: "  ");

        resultado.Should().Be(string.Format(ProgramarAusenciaTool.Mensajes.CampoObligatorio, "identificacion"));
        AsegurarNingunaRequest(fakes);
    }

    [Fact]
    public async Task ProgramarAusencia_DevuelveElChoqueComoTexto_CuandoElDominioRespondeConflicto()
    {
        const string choque = "El periodo choca con la ausencia Vacaciones del 2026-09-10 al 2026-09-14";
        var fakes = CrearTool(statusAusencia: HttpStatusCode.Conflict, cuerpoAusencia: choque);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(ProgramarAusenciaTool.Mensajes.RechazoDelDominio, choque));
    }

    [Fact]
    public async Task ProgramarAusencia_DevuelveElRechazoComoTexto_CuandoElDirectorioFalla()
    {
        var fakes = CrearTool(statusDirectorio: HttpStatusCode.ServiceUnavailable, directorioJson: "");

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(ProgramarAusenciaTool.Mensajes.RechazoDelDominio, "503"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }
}
