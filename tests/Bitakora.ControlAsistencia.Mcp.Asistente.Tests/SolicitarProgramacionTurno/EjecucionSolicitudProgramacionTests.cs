using System.Net;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurnoPorGrupo;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;
using Microsoft.Extensions.Logging;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurno;

public class EjecucionSolicitudProgramacionTests
{
    private const string NombreEvento = "EjecucionSolicitudProgramacion";
    private const string Turno = "Cocina Manana";
    private const string SedeProgramacion = "SUBA";
    private const string RutaTurnos = "/api/programacion/turnos";
    private const string RutaSolicitudes = "/api/programacion/solicitudes";

    private static string TurnosJson => Fixtures.Leer("SolicitarProgramacionTurno", "turnos.json");
    private static string SedeJson => Fixtures.Leer("SolicitarProgramacionTurno", "sede.json");
    private static string DirectorioJson => Fixtures.Leer("SolicitarProgramacionTurno", "directorio.json");

    private static HttpResponseMessage Respuesta(HttpStatusCode status, string cuerpo) =>
        new(status) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private static IReadOnlyList<RegistroCapturado> Registros<T>(LoggerDeCaptura<T> logger) =>
        [.. logger.Registros.Where(r => r.EventId.Name == NombreEvento)];

    private static object Ficha(int numero) => new
    {
        id = $"CC-{numero:D4}",
        nombreCompleto = $"Colab {numero:D4}",
        codigoColaborador = $"C{numero:D4}",
        vigenteDesde = "2025-01-01",
        vigenteHasta = (string?)null,
        etiquetas = Array.Empty<object>(),
        codigoSede = "SUBA"
    };

    private sealed record EntornoLista(
        SolicitarProgramacionTurnoTool Tool, LoggerDeCaptura<SolicitarProgramacionTurnoTool> Logger);

    private static EntornoLista CrearLista(
        Func<string?, HttpResponseMessage>? respuestaPost = null, TimeProvider? reloj = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.Con(SedeJson, HttpStatusCode.OK);
        var (clienteColaboradores, _) = ClienteFalso.Con(DirectorioJson, HttpStatusCode.OK);

        var logger = new LoggerDeCaptura<SolicitarProgramacionTurnoTool>();
        var tool = new SolicitarProgramacionTurnoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores),
            logger,
            reloj ?? TimeProvider.System);
        return new EntornoLista(tool, logger);
    }

    private static Task<string> EjecutarLista(
        SolicitarProgramacionTurnoTool tool,
        string desde = "2026-09-01",
        string hasta = "2026-09-30",
        string identificaciones = "CC-1111,CC-2222,CC-3333",
        CancellationToken ct = default) =>
        tool.Run(null!, desde, hasta, Turno, SedeProgramacion, identificaciones, ct);

    private sealed record EntornoGrupo(
        SolicitarProgramacionTurnoPorGrupoTool Tool, LoggerDeCaptura<SolicitarProgramacionTurnoPorGrupoTool> Logger);

    private static EntornoGrupo CrearGrupo(
        IReadOnlyList<object> fichas,
        Func<string?, HttpResponseMessage>? respuestaPost = null,
        TimeProvider? reloj = null)
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Get, RutaTurnos, HttpStatusCode.OK, TurnosJson);
        programacion.Responde(
            HttpMethod.Post, RutaSolicitudes,
            (_, cuerpo) => respuestaPost?.Invoke(cuerpo) ?? Respuesta(HttpStatusCode.Created, ""));
        var (clienteSedes, _) = ClienteFalso.Con(SedeJson, HttpStatusCode.OK);
        var (clienteColaboradores, _) = ClienteFalso.ConFuncion(_ => Respuesta(
            HttpStatusCode.OK,
            JsonSerializer.Serialize(fichas, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        var logger = new LoggerDeCaptura<SolicitarProgramacionTurnoPorGrupoTool>();
        var tool = new SolicitarProgramacionTurnoPorGrupoTool(
            new ProgramacionApi(clienteProgramacion),
            new SedesApi(clienteSedes),
            new ColaboradoresApi(clienteColaboradores),
            logger,
            reloj ?? TimeProvider.System);
        return new EntornoGrupo(tool, logger);
    }

    private static Task<string> EjecutarGrupo(
        SolicitarProgramacionTurnoPorGrupoTool tool,
        string? sede = null,
        string? etiquetas = "area:cocina",
        string desde = "2026-09-01",
        string hasta = "2026-09-30",
        CancellationToken ct = default) =>
        tool.Run(null!, desde, hasta, Turno, SedeProgramacion, sede, etiquetas, ct);

    [Fact]
    public async Task SolicitarProgramacionTurno_EmiteUnRegistroConModalidadLista_CuandoLlegaALaFaseDeEjecucion()
    {
        var entorno = CrearLista();

        await EjecutarLista(entorno.Tool, ct: TestContext.Current.CancellationToken);

        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Nivel.Should().Be(LogLevel.Information);
        registro.Propiedades["Modalidad"].Should().Be("lista");
        registro.Propiedades["TamanoResuelto"].Should().Be(3);
        registro.Propiedades["Programados"].Should().Be(2);
        registro.Propiedades["Omitidos"].Should().Be(1);
        registro.Propiedades["Fallidos"].Should().Be(0);
        registro.Propiedades["Turno"].Should().Be("Cocina Manana");
        registro.Propiedades["SedeDeProgramacion"].Should().Be("SUBA");
        registro.Propiedades["Desde"].Should().Be(new DateOnly(2026, 9, 1));
        registro.Propiedades["Hasta"].Should().Be(new DateOnly(2026, 9, 30));
        registro.Propiedades["DiasVentana"].Should().Be(30);
        registro.Propiedades.GetValueOrDefault("Sede").Should().BeNull();
        registro.Propiedades.GetValueOrDefault("Etiquetas").Should().BeNull();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EmiteUnRegistroConModalidadGrupo_CuandoLlegaALaFaseDeEjecucion()
    {
        var entorno = CrearGrupo([Ficha(1), Ficha(2), Ficha(3)]);

        await EjecutarGrupo(entorno.Tool, sede: "SUBA", etiquetas: "area:cocina",
            ct: TestContext.Current.CancellationToken);

        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Nivel.Should().Be(LogLevel.Information);
        registro.Propiedades["Modalidad"].Should().Be("grupo");
        registro.Propiedades["TamanoResuelto"].Should().Be(3);
        registro.Propiedades["Programados"].Should().Be(3);
        registro.Propiedades["Omitidos"].Should().Be(0);
        registro.Propiedades["Fallidos"].Should().Be(0);
        registro.Propiedades["Turno"].Should().Be("Cocina Manana");
        registro.Propiedades["SedeDeProgramacion"].Should().Be("SUBA");
        registro.Propiedades["DiasVentana"].Should().Be(30);
        registro.Propiedades["Sede"].Should().Be("SUBA");
        registro.Propiedades["Etiquetas"].Should().Be("area:cocina");
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoIncluyeDatosPersonales_CuandoEmiteElRegistro()
    {
        var entorno = CrearGrupo([Ficha(1)]);

        await EjecutarGrupo(entorno.Tool, ct: TestContext.Current.CancellationToken);

        var valores = Registros(entorno.Logger).Single().Propiedades.Values
            .Select(v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
        valores.Should().NotContain(v => v!.Contains("CC-0001") || v.Contains("Colab 0001"));
        Registros(entorno.Logger).Single().Propiedades.Keys
            .Should().NotContain(["Identificacion", "Identificaciones", "Nombre", "Nombres"]);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_ReportaLosFallidos_CuandoElDominioRechazaUnPost()
    {
        var entorno = CrearLista(_ => Respuesta(HttpStatusCode.Conflict, "rechazado"));

        await EjecutarLista(entorno.Tool, ct: TestContext.Current.CancellationToken);

        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Propiedades["Programados"].Should().Be(0);
        registro.Propiedades["Fallidos"].Should().Be(2);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_MideDuracionEnMilisegundos_ConElTimeProviderInyectado()
    {
        var reloj = new RelojAvanzable();
        var entorno = CrearGrupo([Ficha(1)], respuestaPost: _ =>
        {
            reloj.Avanzar(TimeSpan.FromMilliseconds(5000));
            return Respuesta(HttpStatusCode.Created, "");
        }, reloj: reloj);

        await EjecutarGrupo(entorno.Tool, ct: TestContext.Current.CancellationToken);

        Convert.ToInt64(Registros(entorno.Logger).Single().Propiedades["DuracionMs"])
            .Should().Be(5000);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoEmiteRegistro_CuandoUnaEtiquetaEstaMalFormada()
    {
        var entorno = CrearGrupo([Ficha(1)]);

        await EjecutarGrupo(entorno.Tool, etiquetas: "area", ct: TestContext.Current.CancellationToken);

        Registros(entorno.Logger).Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_NoEmiteRegistro_CuandoLaVentanaExcedeElMaximo()
    {
        var entorno = CrearGrupo([Ficha(1)]);

        await EjecutarGrupo(entorno.Tool, desde: "2026-09-01", hasta: "2026-12-31",
            ct: TestContext.Current.CancellationToken);

        Registros(entorno.Logger).Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_NoEmiteRegistro_CuandoLaVentanaEstaInvertida()
    {
        var entorno = CrearLista();

        await EjecutarLista(entorno.Tool, desde: "2026-09-30", hasta: "2026-09-01",
            ct: TestContext.Current.CancellationToken);

        Registros(entorno.Logger).Should().BeEmpty();
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EmiteRegistroConCeros_CuandoElGrupoEstaVacio()
    {
        var entorno = CrearGrupo([]);

        await EjecutarGrupo(entorno.Tool, ct: TestContext.Current.CancellationToken);

        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Propiedades["Modalidad"].Should().Be("grupo");
        registro.Propiedades["TamanoResuelto"].Should().Be(0);
        registro.Propiedades["Programados"].Should().Be(0);
    }

    [Fact]
    public async Task SolicitarProgramacionTurnoPorGrupo_EmiteRegistroConConteosParciales_CuandoLaEjecucionLanza()
    {
        var entorno = CrearGrupo([Ficha(1)], _ => throw new InvalidOperationException("falla de infraestructura"));

        var act = () => EjecutarGrupo(entorno.Tool, ct: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<Exception>();
        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Propiedades["TamanoResuelto"].Should().Be(1);
        registro.Propiedades["Programados"].Should().Be(0);
    }

    [Fact]
    public async Task SolicitarProgramacionTurno_EmiteRegistroConConteosParciales_CuandoSeCancelaDuranteLosPost()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var entorno = CrearLista(_ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        var act = () => EjecutarLista(entorno.Tool, ct: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var registro = Registros(entorno.Logger).Should().ContainSingle().Subject;
        registro.Propiedades["Modalidad"].Should().Be("lista");
        registro.Propiedades["TamanoResuelto"].Should().Be(3);
        registro.Propiedades["Programados"].Should().Be(0);
    }
}
