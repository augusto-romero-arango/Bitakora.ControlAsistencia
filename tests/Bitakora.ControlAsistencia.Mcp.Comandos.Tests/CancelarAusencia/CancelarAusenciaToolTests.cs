using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Comandos.CancelarAusencia;
using Bitakora.ControlAsistencia.Mcp.Comandos.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Comandos.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Comandos.Tests.CancelarAusencia;

public class CancelarAusenciaToolTests
{
    private const string RutaAusencias = "/api/programacion/colaboradores/AR01/ausencias";
    private const string IdIncapacidad = "0190aaaa-0000-7000-8000-000000000001";
    private const string IdVacaciones = "0190aaaa-0000-7000-8000-000000000002";

    private static string DirectorioJson => Fixtures.Leer("CancelarAusencia", "directorio.json");
    private static string SoloIncapacidadJson => Fixtures.Leer("CancelarAusencia", "ausencia-incapacidad.json");
    private static string DosAusenciasJson => Fixtures.Leer("CancelarAusencia", "ausencias-dos.json");

    private sealed record Fakes(CancelarAusenciaTool Tool, HandlerPorRuta Programacion, HandlerEnlatado Colaboradores);

    private static Fakes CrearTool(
        string ausenciasJson,
        HttpStatusCode statusCancelar = HttpStatusCode.NoContent,
        string cuerpoCancelar = "")
    {
        var (clienteProgramacion, handlerProgramacion) = ClienteFalso.ConRutas();
        handlerProgramacion.Responde(new HttpMethod("QUERY"), RutaAusencias, HttpStatusCode.OK, ausenciasJson);
        handlerProgramacion.RespondeConPrefijo(HttpMethod.Post, RutaAusencias + "/", statusCancelar, cuerpoCancelar);

        var (clienteColaboradores, handlerColaboradores) = ClienteFalso.Con(DirectorioJson);
        var tool = new CancelarAusenciaTool(
            new ProgramacionApi(clienteProgramacion), new ColaboradoresApi(clienteColaboradores));
        return new Fakes(tool, handlerProgramacion, handlerColaboradores);
    }

    private static Task<string> Ejecutar(
        CancelarAusenciaTool tool,
        string identificacion = "CC-1111",
        string desde = "2026-09-20",
        string hasta = "2026-09-30",
        bool? completa = null) =>
        tool.Run(
            context: null!,
            identificacion: identificacion,
            desde: desde,
            hasta: hasta,
            completa: completa,
            ct: TestContext.Current.CancellationToken);

    private static List<(string Ruta, string[] Fechas)> Cancelaciones(Fakes fakes) =>
        [.. fakes.Programacion.Requests
            .Where(r => r.Metodo == HttpMethod.Post)
            .Select(r => (r.Ruta, Fechas: JsonNode.Parse(r.Cuerpo!)!["fechas"]!.AsArray()
                .Select(f => f!.GetValue<string>()).ToArray()))];

    private static string[] Rango(string desde, string hasta)
    {
        var inicio = DateOnly.Parse(desde);
        var fin = DateOnly.Parse(hasta);
        return [.. Enumerable.Range(0, fin.DayNumber - inicio.DayNumber + 1)
            .Select(i => inicio.AddDays(i).ToString("yyyy-MM-dd"))];
    }

    // CA-4: "volvio el 20" sobre una incapacidad 15-24 -> solo 20-24.
    [Fact]
    public async Task CancelarAusencia_CancelaSoloLosDiasDelPeriodo_CuandoNoEsCompleta()
    {
        var fakes = CrearTool(SoloIncapacidadJson);

        var resultado = await Ejecutar(fakes.Tool);

        var consulta = fakes.Programacion.Requests.First();
        consulta.Metodo.Method.Should().Be("QUERY");
        consulta.Ruta.Should().Be(RutaAusencias);
        var filtro = JsonNode.Parse(consulta.Cuerpo!)!;
        filtro["desde"]!.GetValue<string>().Should().Be("2026-09-20");
        filtro["hasta"]!.GetValue<string>().Should().Be("2026-09-30");

        var cancelaciones = Cancelaciones(fakes);
        cancelaciones.Should().ContainSingle();
        cancelaciones[0].Ruta.Should().Be($"{RutaAusencias}/{IdIncapacidad}:cancelar");
        cancelaciones[0].Fechas.Should().Equal(Rango("2026-09-20", "2026-09-24"));

        var json = JsonNode.Parse(resultado)!;
        json["resultado"]!.GetValue<string>().Should().Be(CancelarAusenciaTool.Mensajes.ResultadoAusenciasCanceladas);
        json["colaborador"]!.GetValue<string>().Should().Be("Ana Ruiz");
        var canceladas = json["canceladas"]!.AsArray();
        canceladas.Should().ContainSingle();
        canceladas[0]!["motivo"]!.GetValue<string>().Should().Be("IncapacidadMedica");
        canceladas[0]!["dias"]!.GetValue<int>().Should().Be(5);
    }

    [Fact]
    public async Task CancelarAusencia_IntersectaCadaAusenciaConElPeriodo_CuandoNoEsCompletaYHayDos()
    {
        var fakes = CrearTool(DosAusenciasJson);

        await Ejecutar(fakes.Tool, completa: false);

        var cancelaciones = Cancelaciones(fakes);
        cancelaciones.Should().HaveCount(2);
        cancelaciones.Single(c => c.Ruta.Contains(IdIncapacidad)).Fechas
            .Should().Equal(Rango("2026-09-20", "2026-09-24"));
        cancelaciones.Single(c => c.Ruta.Contains(IdVacaciones)).Fechas
            .Should().Equal(Rango("2026-09-28", "2026-09-30"));
    }

    // CA-5: completa = true -> una cancelacion por ausencia con todos sus tramos vigentes
    // (la ausencia de vacaciones tiene un hueco 3-5 de octubre ya cancelado).
    [Fact]
    public async Task CancelarAusencia_CancelaCadaAusenciaEnteraConSusTramosVigentes_CuandoEsCompleta()
    {
        var fakes = CrearTool(DosAusenciasJson);

        var resultado = await Ejecutar(fakes.Tool, completa: true);

        var cancelaciones = Cancelaciones(fakes);
        cancelaciones.Should().HaveCount(2);
        cancelaciones.Single(c => c.Ruta == $"{RutaAusencias}/{IdIncapacidad}:cancelar").Fechas
            .Should().Equal(Rango("2026-09-15", "2026-09-24"));
        cancelaciones.Single(c => c.Ruta == $"{RutaAusencias}/{IdVacaciones}:cancelar").Fechas
            .Should().Equal([.. Rango("2026-09-28", "2026-10-02"), .. Rango("2026-10-06", "2026-10-09")]);

        var canceladas = JsonNode.Parse(resultado)!["canceladas"]!.AsArray();
        canceladas.Should().HaveCount(2);
        canceladas.Single(c => c!["motivo"]!.GetValue<string>() == "Vacaciones")!["dias"]!
            .GetValue<int>().Should().Be(9);
    }

    // CA-6
    [Fact]
    public async Task CancelarAusencia_ExplicaYNoCancela_CuandoNoHayAusenciasEnElPeriodo()
    {
        var fakes = CrearTool("[]");

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(
            CancelarAusenciaTool.Mensajes.SinAusenciasEnElPeriodo, "Ana Ruiz", "2026-09-20 a 2026-09-30"));
        Cancelaciones(fakes).Should().BeEmpty();
    }

    // La consulta trae la ausencia 28-sep..09-oct, pero 3-5 de octubre ya estan canceladas: la
    // interseccion con el periodo queda vacia y no hay nada que cancelar.
    [Fact]
    public async Task CancelarAusencia_ExplicaYNoCancela_CuandoElPeriodoCaeEnUnHuecoYaCancelado()
    {
        var fakes = CrearTool(DosAusenciasJson);

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-10-03", hasta: "2026-10-05");

        resultado.Should().Be(string.Format(
            CancelarAusenciaTool.Mensajes.SinAusenciasEnElPeriodo, "Ana Ruiz", "2026-10-03 a 2026-10-05"));
        Cancelaciones(fakes).Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarAusencia_RechazaSinEscribir_CuandoElColaboradorNoEstaEnElDirectorio()
    {
        var fakes = CrearTool(SoloIncapacidadJson);

        var resultado = await Ejecutar(fakes.Tool, identificacion: "CC-9999");

        resultado.Should().Be(string.Format(CancelarAusenciaTool.Mensajes.ColaboradorNoEncontrado, "CC-9999"));
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarAusencia_RechazaSinEscribir_CuandoLaFechaTieneFormatoInvalido()
    {
        var fakes = CrearTool(SoloIncapacidadJson);

        var resultado = await Ejecutar(fakes.Tool, hasta: "30-09-2026");

        resultado.Should().Be(string.Format(CancelarAusenciaTool.Mensajes.FechaInvalida, "hasta", "30-09-2026"));
        fakes.Programacion.Requests.Should().BeEmpty();
        fakes.Colaboradores.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task CancelarAusencia_RechazaSinEscribir_CuandoDesdeEsPosteriorAHasta()
    {
        var fakes = CrearTool(SoloIncapacidadJson);

        var resultado = await Ejecutar(fakes.Tool, desde: "2026-09-30", hasta: "2026-09-20");

        resultado.Should().Be(CancelarAusenciaTool.Mensajes.PeriodoInvertido);
        fakes.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelarAusencia_DevuelveElRechazoComoTexto_CuandoElDominioRechazaLaCancelacion()
    {
        const string rechazo = "La ausencia no existe";
        var fakes = CrearTool(SoloIncapacidadJson, HttpStatusCode.NotFound, rechazo);

        var resultado = await Ejecutar(fakes.Tool);

        resultado.Should().Be(string.Format(CancelarAusenciaTool.Mensajes.RechazoDelDominio, rechazo));
    }
}
