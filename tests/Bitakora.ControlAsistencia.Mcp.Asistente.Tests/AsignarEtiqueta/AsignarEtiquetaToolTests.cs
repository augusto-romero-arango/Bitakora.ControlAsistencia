using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AsignarEtiqueta;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarJornada;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AsignarEtiqueta;

public class AsignarEtiquetaToolTests
{
    private const string MotivoDel409 = "La vinculacion del colaborador ya termino";
    private const string MotivoDel404 = "El colaborador no existe";

    private static (AsignarEtiquetaTool Tool, EtiquetasFalso Colaboradores) CrearEntorno(
        IReadOnlyList<object> directorio,
        Func<string, HttpResponseMessage>? respuestaEscritura = null,
        bool directorioFalla = false)
    {
        var colaboradores = new EtiquetasFalso(directorio, respuestaEscritura, directorioFalla);
        var api = new ColaboradoresApi(
            new HttpClient(colaboradores) { BaseAddress = new Uri("https://dominio.falso.local") });
        return (new AsignarEtiquetaTool(api, new ResolutorCandidatosPorLista(api)), colaboradores);
    }

    private static Task<string> Ejecutar(
        AsignarEtiquetaTool tool, string categoria, string valor, string identificaciones) =>
        tool.Run(null!, categoria, valor, identificaciones, TestContext.Current.CancellationToken);

    [Fact]
    public async Task AsignarEtiqueta_EnviaPutConValorACadaColaborador_CuandoElDirectorioLosEncuentra()
    {
        var (tool, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Entrada(1), ColaboradoresFalso.Entrada(2)]);

        var resultado = await Ejecutar(tool, "area de trabajo", "SERVICIO AL USUARIO", "CC-1, CC-2");

        colaboradores.Escrituras.Select(e => (e.Metodo, e.Ruta)).Should().BeEquivalentTo(
        [
            ("PUT", "/api/colaboradores/CC-1/etiquetas/area%20de%20trabajo"),
            ("PUT", "/api/colaboradores/CC-2/etiquetas/area%20de%20trabajo")
        ]);
        colaboradores.Escrituras.Should().OnlyContain(
            e => e.Cuerpo!["valor"]!.GetValue<string>() == "SERVICIO AL USUARIO");

        var json = JsonNode.Parse(resultado)!.AsObject();
        json["resultado"]!.GetValue<string>().Should().Be(AsignarEtiquetaTool.Mensajes.ResultadoEtiquetaAsignada);
        json["etiqueta"]!.GetValue<string>().Should().Be("area de trabajo:SERVICIO AL USUARIO");
        json["asignados"]!.GetValue<int>().Should().Be(2);
        json["nota"]!.GetValue<string>().Should().Be(AsignarEtiquetaTool.Mensajes.NotaVisibilidadEventual);
        json.ContainsKey("fallidos").Should().BeFalse();
        json.ContainsKey("no_encontrados").Should().BeFalse();
    }

    [Fact]
    public async Task AsignarEtiqueta_ReportaNoEncontradosYFallidosSinDetenerAlResto_CuandoHayIdentificacionesAusentesYRechazos()
    {
        var (tool, colaboradores) = CrearEntorno(
            [ColaboradoresFalso.Entrada(1), ColaboradoresFalso.Entrada(2), ColaboradoresFalso.Entrada(3)],
            respuestaEscritura: id => id switch
            {
                "CC-2" => EtiquetasFalso.Json(HttpStatusCode.Conflict, MotivoDel409),
                "CC-3" => EtiquetasFalso.Json(HttpStatusCode.NotFound, MotivoDel404),
                _ => new HttpResponseMessage(HttpStatusCode.NoContent)
            });

        var resultado = await Ejecutar(tool, "area", "NORTE", "CC-1, CC-2, CC-3, CC-9");

        colaboradores.IdentificacionesEscritas.Should().BeEquivalentTo(["CC-1", "CC-2", "CC-3"]);
        var json = JsonNode.Parse(resultado)!;
        json["asignados"]!.GetValue<int>().Should().Be(1);
        json["no_encontrados"]!.AsArray().Select(n => n!.GetValue<string>()).Should().BeEquivalentTo(["CC-9"]);
        var fallidos = json["fallidos"]!.AsArray().Select(n => n!).ToList();
        fallidos.Should().HaveCount(2);
        fallidos.Single(f => f["identificacion"]!.GetValue<string>() == "CC-2")["motivo"]!
            .GetValue<string>().Should().Contain(MotivoDel409);
        fallidos.Single(f => f["identificacion"]!.GetValue<string>() == "CC-3")["motivo"]!
            .GetValue<string>().Should().Contain(MotivoDel404);
    }

    [Fact]
    public async Task AsignarEtiqueta_RespondeCampoObligatorioSinLlamar_CuandoLaCategoriaEstaEnBlanco()
    {
        var (tool, colaboradores) = CrearEntorno([ColaboradoresFalso.Entrada(1)]);

        var resultado = await Ejecutar(tool, "  ", "NORTE", "CC-1");

        resultado.Should().Be(string.Format(AsignarEtiquetaTool.Mensajes.CampoObligatorio, "categoria"));
        colaboradores.Consultas.Should().BeEmpty();
        colaboradores.Escrituras.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarEtiqueta_RespondeCampoObligatorioSinLlamar_CuandoElValorEstaVacio()
    {
        var (tool, colaboradores) = CrearEntorno([ColaboradoresFalso.Entrada(1)]);

        var resultado = await Ejecutar(tool, "area", "", "CC-1");

        resultado.Should().Be(string.Format(AsignarEtiquetaTool.Mensajes.CampoObligatorio, "valor"));
        colaboradores.Consultas.Should().BeEmpty();
        colaboradores.Escrituras.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarEtiqueta_RespondeCampoObligatorioSinLlamar_CuandoLasIdentificacionesEstanEnBlanco()
    {
        var (tool, colaboradores) = CrearEntorno([ColaboradoresFalso.Entrada(1)]);

        var resultado = await Ejecutar(tool, "area", "NORTE", " , ");

        resultado.Should().Be(string.Format(AsignarEtiquetaTool.Mensajes.CampoObligatorio, "identificaciones"));
        colaboradores.Consultas.Should().BeEmpty();
        colaboradores.Escrituras.Should().BeEmpty();
    }

    [Fact]
    public async Task AsignarEtiqueta_RespondeRechazoDelDominio_CuandoFallaLaLecturaDelDirectorio()
    {
        var (tool, colaboradores) = CrearEntorno([], directorioFalla: true);

        var resultado = await Ejecutar(tool, "area", "NORTE", "CC-1");

        resultado.Should().Be(string.Format(AsignarEtiquetaTool.Mensajes.RechazoDelDominio, "directorio caido"));
        colaboradores.Escrituras.Should().BeEmpty();
    }
}
