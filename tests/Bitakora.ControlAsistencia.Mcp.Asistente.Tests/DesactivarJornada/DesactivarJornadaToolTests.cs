using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.DesactivarJornada;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.DesactivarJornada;

public class DesactivarJornadaToolTests
{
    private const string JornadaTexto = "01a08700-0000-7000-9000-000000000001";
    private static readonly string RutaDesactivar = $"/api/programacion/jornadas/{JornadaTexto}:desactivar";
    private const string RutaFichas = "/api/colaboradores/fichas";
    private static readonly HttpMethod Query = new("QUERY");

    // 2026-10-13 03:00 UTC es 2026-10-12 22:00 en Bogota: distingue "hoy en Bogota" de "hoy en UTC".
    private static readonly TimeProvider RelojDeNoche = new RelojFalso(new DateTimeOffset(2026, 10, 13, 3, 0, 0, TimeSpan.Zero));

    private sealed record Entorno(DesactivarJornadaTool Tool, HandlerPorRuta Programacion, HandlerPorRuta Colaboradores);

    private static Entorno CrearEntorno(
        string fichasJson,
        HttpStatusCode statusFichas = HttpStatusCode.OK,
        HttpStatusCode statusDesactivar = HttpStatusCode.NoContent,
        string cuerpoDesactivar = "")
    {
        var (clienteProgramacion, programacion) = ClienteFalso.ConRutas();
        programacion.Responde(HttpMethod.Post, RutaDesactivar, statusDesactivar, cuerpoDesactivar);

        var (clienteColaboradores, colaboradores) = ClienteFalso.ConRutas();
        colaboradores.Responde(Query, RutaFichas, statusFichas, fichasJson);

        var tool = new DesactivarJornadaTool(
            new ProgramacionApi(clienteProgramacion), new ColaboradoresApi(clienteColaboradores), RelojDeNoche);
        return new Entorno(tool, programacion, colaboradores);
    }

    private static Task<string> Ejecutar(DesactivarJornadaTool tool, string jornadaId = JornadaTexto) =>
        tool.Run(null!, jornadaId, TestContext.Current.CancellationToken);

    private static string Fichas(string nombre) => Fixtures.Leer("DesactivarJornada", nombre);

    [Fact]
    public async Task DesactivarJornada_RespondeFraseSinLlamadasHttp_CuandoElJornadaIdNoEsGuid()
    {
        var entorno = CrearEntorno(Fichas("fichas-vacias.json"));

        var resultado = await Ejecutar(entorno.Tool, "no-es-guid");

        resultado.Should().Be(string.Format(DesactivarJornadaTool.Mensajes.JornadaIdInvalido, "no-es-guid"));
        entorno.Programacion.Requests.Should().BeEmpty();
        entorno.Colaboradores.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesactivarJornada_NoDesactivaYCuentaTodosLosVigentes_CuandoLaUsanTresColaboradoresUnoConTerminacion()
    {
        var entorno = CrearEntorno(Fichas("fichas-tres-una-con-terminacion.json"));

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Contain(string.Format(DesactivarJornadaTool.Mensajes.JornadaEnUsoVariosColaboradores, 3));
        resultado.Should().Contain(DesactivarJornadaTool.Mensajes.UnaTerminacionRegistrada);
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesactivarJornada_NoMencionaTerminaciones_CuandoLaUsanDosColaboradoresSinVigenteHasta()
    {
        var entorno = CrearEntorno(Fichas("fichas-dos-sin-terminacion.json"));

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Contain(string.Format(DesactivarJornadaTool.Mensajes.JornadaEnUsoVariosColaboradores, 2));
        resultado.Should().NotContain(DesactivarJornadaTool.Mensajes.UnaTerminacionRegistrada);
        resultado.Should().NotContain(string.Format(DesactivarJornadaTool.Mensajes.VariasTerminacionesRegistradas, 1));
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesactivarJornada_UsaLaFraseSingular_CuandoLaUsaUnSoloColaboradorConTerminacion()
    {
        var entorno = CrearEntorno(Fichas("fichas-una-con-terminacion.json"));

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Contain(DesactivarJornadaTool.Mensajes.JornadaEnUsoUnColaborador);
        resultado.Should().Contain(DesactivarJornadaTool.Mensajes.UnaTerminacionRegistrada);
        entorno.Programacion.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DesactivarJornada_ConsultaFichasConHoyEnBogotaJornadaIdYSinTake_CuandoElJornadaIdEsValido()
    {
        var entorno = CrearEntorno(Fichas("fichas-vacias.json"));

        await Ejecutar(entorno.Tool);

        var consulta = entorno.Colaboradores.Requests.Should().ContainSingle().Subject;
        consulta.Metodo.Should().Be(Query);
        var cuerpo = JsonNode.Parse(consulta.Cuerpo!)!.AsObject();
        cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-12");
        cuerpo["jornadaId"]!.GetValue<string>().Should().Be(JornadaTexto);
        cuerpo.ContainsKey("take").Should().BeFalse();
    }

    [Fact]
    public async Task DesactivarJornada_EnviaElPostYRespondeExito_CuandoNadieLaUsaYElServidorDevuelve204()
    {
        var entorno = CrearEntorno(Fichas("fichas-vacias.json"));

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Contain(DesactivarJornadaTool.Mensajes.ResultadoJornadaDesactivada);
        entorno.Programacion.Requests.Should().ContainSingle(r =>
            r.Metodo == HttpMethod.Post && r.Ruta == RutaDesactivar);
    }

    [Fact]
    public async Task DesactivarJornada_TraduceElRechazoDelDominio_Cuando409DelServidor()
    {
        const string cuerpo = "La Jornada es la predeterminada";
        var entorno = CrearEntorno(
            Fichas("fichas-vacias.json"), statusDesactivar: HttpStatusCode.Conflict, cuerpoDesactivar: cuerpo);

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Be(string.Format(DesactivarJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task DesactivarJornada_TraduceElRechazoDelDominio_Cuando404DelServidor()
    {
        const string cuerpo = "La Jornada no existe";
        var entorno = CrearEntorno(
            Fichas("fichas-vacias.json"), statusDesactivar: HttpStatusCode.NotFound, cuerpoDesactivar: cuerpo);

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Be(string.Format(DesactivarJornadaTool.Mensajes.RechazoDelDominio, cuerpo));
    }

    [Fact]
    public async Task DesactivarJornada_RespondeElRechazoDelDominioSinPost_CuandoLaLecturaDeFichasFalla()
    {
        var entorno = CrearEntorno("", statusFichas: HttpStatusCode.ServiceUnavailable);

        var resultado = await Ejecutar(entorno.Tool);

        resultado.Should().Be(string.Format(DesactivarJornadaTool.Mensajes.RechazoDelDominio, "503"));
        entorno.Programacion.Requests.Should().BeEmpty();
    }
}
