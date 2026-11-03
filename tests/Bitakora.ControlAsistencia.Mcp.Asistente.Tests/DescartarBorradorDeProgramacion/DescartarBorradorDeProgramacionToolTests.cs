using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.DescartarBorradorDeProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.DescartarBorradorDeProgramacion;

public class DescartarBorradorDeProgramacionToolTests
{
    private const string RutaActo = "/api/programacion/colaboradores/AR01/borrador:descartar";
    private const string RutaVista = "/api/programacion/borradores";
    private static readonly HttpMethod Query = new("QUERY");

    private static readonly TimeProvider RelojDeNoche =
        new RelojFalso(new DateTimeOffset(2026, 11, 4, 3, 0, 0, TimeSpan.Zero));

    private sealed record Fakes(
        DescartarBorradorDeProgramacionTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Colaboradores);

    private static Fakes CrearTool(
        string vistaJson,
        HttpStatusCode statusVista = HttpStatusCode.OK,
        Func<HttpRequestMessage, string?, HttpResponseMessage>? acto = null,
        string? directorioJson = null,
        HttpStatusCode statusDirectorio = HttpStatusCode.OK)
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(Query, RutaVista, statusVista, vistaJson);
        handlerProgramacion.Responde(
            HttpMethod.Post, RutaActo, acto ?? ((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent)));

        var (clienteColaboradores, handlerColaboradores) =
            ClienteFalso.Con(
                directorioJson ?? Fixtures.Leer("DescartarBorradorDeProgramacion", "directorio.json"), statusDirectorio);
        var tool = new DescartarBorradorDeProgramacionTool(
            new ProgramacionApi(clienteProgramacion), new ColaboradoresApi(clienteColaboradores), RelojDeNoche);
        return new Fakes(tool, handlerProgramacion, handlerColaboradores);
    }

    private static Task<string> Ejecutar(
        DescartarBorradorDeProgramacionTool tool, string identificacion = "CC-1111", string? desde = null, string? hasta = null) =>
        tool.Run(
            context: null!,
            identificacion: identificacion,
            desde: desde,
            hasta: hasta,
            ct: TestContext.Current.CancellationToken);

    private static string Vista(string nombre) => Fixtures.Leer("DescartarBorradorDeProgramacion", nombre);

    [Fact]
    public async Task DescartarBorradorDeProgramacion_LeeLaVistaConElColaboradorYHoyEnBogota_CuandoNoSePasaPeriodo()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        await Ejecutar(fakes.Tool);

        var lectura = fakes.Programacion.Requests.Should().Contain(r => r.Metodo == Query).Subject;
        lectura.Ruta.Should().Be(RutaVista);
        var cuerpo = JsonNode.Parse(lectura.Cuerpo!)!;
        cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-11-03");
        cuerpo["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("AR01");
        cuerpo["desde"].Should().BeNull();
        cuerpo["hasta"].Should().BeNull();
        cuerpo["take"].Should().BeNull();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_LlamaUnaVezAlActoConElPrimerYUltimoDiaPendiente_CuandoNoSePasaPeriodo()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool);

        var llamada = fakes.Programacion.Requests.Where(r => r.Metodo == HttpMethod.Post).Should().ContainSingle().Subject;
        llamada.Ruta.Should().Be(RutaActo);
        var cuerpo = JsonNode.Parse(llamada.Cuerpo!)!;
        cuerpo["desde"]!.GetValue<string>().Should().Be("2026-09-02");
        cuerpo["hasta"]!.GetValue<string>().Should().Be("2026-09-10");

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(DescartarBorradorDeProgramacionTool.Mensajes.ResultadoBorradorDescartado);
        json["colaborador"]!.GetValue<string>().Should().Be("Ana Ruiz");
        json["desde"]!.GetValue<string>().Should().Be("2026-09-02");
        json["hasta"]!.GetValue<string>().Should().Be("2026-09-10");
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RespondeSinBorradorPendienteYNoLlamaAlActo_CuandoLaVistaNoDevuelveAlColaborador()
    {
        var fakes = CrearTool(Vista("borradores-vacio.json"));

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.SinBorradorPendiente, "Ana Ruiz"));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_CortaEnTramosDe366Dias_CuandoElPeriodoPendientePasaDe366()
    {
        var fakes = CrearTool(Vista("borradores-largo.json"));

        var resultado = await Ejecutar(fakes.Tool);

        var llamadas = fakes.Programacion.Requests.Where(r => r.Metodo == HttpMethod.Post).ToList();
        llamadas.Should().HaveCount(2);
        var primero = JsonNode.Parse(llamadas[0].Cuerpo!)!;
        var segundo = JsonNode.Parse(llamadas[1].Cuerpo!)!;
        primero["desde"]!.GetValue<string>().Should().Be("2026-01-01");
        primero["hasta"]!.GetValue<string>().Should().Be("2027-01-01");
        segundo["desde"]!.GetValue<string>().Should().Be("2027-01-02");
        segundo["hasta"]!.GetValue<string>().Should().Be("2027-03-15");

        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-01-01");
        json["hasta"]!.GetValue<string>().Should().Be("2027-03-15");
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_NoLlamaALosTramosSiguientesYDevuelveElRechazo_CuandoUnTramoEsRechazado()
    {
        const string rechazo = "Tramo rechazado";
        var fakes = CrearTool(
            Vista("borradores-largo.json"),
            acto: (_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(rechazo, Encoding.UTF8, "application/json")
            });

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.RechazoDelDominio, rechazo));
        fakes.Programacion.Requests.Where(r => r.Metodo == HttpMethod.Post).Should().ContainSingle();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_DevuelveElRechazoComoTexto_CuandoLaVistaRechaza()
    {
        const string rechazo = "Vista no disponible";
        var fakes = CrearTool(rechazo, HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.RechazoDelDominio, rechazo));
        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == HttpMethod.Post);
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_ConservaElPeriodoDadoSinLeerLaVista_CuandoSePasaPeriodo()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-01", hasta: "2026-09-03");

        fakes.Programacion.Requests.Should().NotContain(r => r.Metodo == Query);
        var llamada = fakes.Programacion.Requests.Should().ContainSingle().Subject;
        llamada.Ruta.Should().Be(RutaActo);
        var json = JsonNode.Parse(resultado)!;
        json["desde"]!.GetValue<string>().Should().Be("2026-09-01");
        json["hasta"]!.GetValue<string>().Should().Be("2026-09-03");
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoLlegaSoloDesde()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-01");

        resultado.Should().Be(DescartarBorradorDeProgramacionTool.Mensajes.PeriodoIncompleto);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoLlegaSoloHasta()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, hasta: "2026-09-03");

        resultado.Should().Be(DescartarBorradorDeProgramacionTool.Mensajes.PeriodoIncompleto);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoFaltaIdentificacion()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, identificacion: " ");

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.CampoObligatorio, "identificacion"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoLaFechaTieneFormatoInvalido()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-01", hasta: "03-09-2026");

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.FechaInvalida, "hasta", "03-09-2026"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-30", hasta: "2026-09-20");

        resultado.Should().Be(DescartarBorradorDeProgramacionTool.Mensajes.PeriodoInvertido);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoElPeriodoDadoDura367Dias()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-01-01", hasta: "2027-01-02");

        resultado.Should().Be(DescartarBorradorDeProgramacionTool.Mensajes.PeriodoExcedeElMaximo);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_RechazaSinLlamar_CuandoLaIdentificacionNoEstaEnElDirectorio()
    {
        var fakes = CrearTool(Vista("borradores.json"));

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-9999");

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.ColaboradorNoEncontrado, "CC-9999"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DescartarBorradorDeProgramacion_DevuelveElRechazoComoTextoSinLlamar_CuandoElDirectorioRechaza()
    {
        const string rechazo = "Directorio no disponible";
        var fakes = CrearTool(
            Vista("borradores.json"), directorioJson: rechazo, statusDirectorio: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-01", hasta: "2026-09-03");

        resultado.Should().Be(string.Format(DescartarBorradorDeProgramacionTool.Mensajes.RechazoDelDominio, rechazo));
        fakes.Programacion.Requests.Should().BeEmpty();
    }
}
