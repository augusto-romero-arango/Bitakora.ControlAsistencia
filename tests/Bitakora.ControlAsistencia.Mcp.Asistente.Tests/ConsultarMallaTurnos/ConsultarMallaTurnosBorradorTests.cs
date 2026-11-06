using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarMallaTurnos;

public class ConsultarMallaTurnosBorradorTests
{
    private static readonly RelojFalso Hoy = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-5)));

    private static (ConsultarMallaTurnosTool Tool, HandlerEnlatado Malla, HandlerEnlatado Vista) Armar(
        string? jsonVista = null, HttpStatusCode statusVista = HttpStatusCode.OK, string? jsonMalla = null)
    {
        var (clienteMalla, handlerMalla) = ClienteFalso.Con(jsonMalla ?? Leer("malla-semana.json"));
        var (clienteVista, handlerVista) = ClienteFalso.Con(jsonVista ?? Leer("borradores-semana.json"), statusVista);
        var (clienteFichas, _) = ClienteFalso.Con(Leer("fichas-grupo.json"));
        var tool = new ConsultarMallaTurnosTool(
            new ControlHorasApi(clienteMalla),
            new ProgramacionApi(clienteVista),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            Hoy);
        return (tool, handlerMalla, handlerVista);
    }

    private static string Leer(string nombre) => Fixtures.Leer("ConsultarMallaTurnos", nombre);

    private static async Task<List<string>> DiasDe(ConsultarMallaTurnosTool tool, int colaborador)
    {
        var resultado = await tool.Run(
            null!, "2026-10-14", null, null, null, null, TestContext.Current.CancellationToken);
        return [.. JsonNode.Parse(resultado)!["colaboradores"]![colaborador]!["dias"]!.AsArray()
            .Select(d => d!.GetValue<string>())];
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraElTurnoDelBorradorMarcado_CuandoLaCasillaEstaSinProgramarYHayBorrador()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 0);

        dias[6].Should().Be("2026-10-18 dom: " + string.Format(
            ConsultarMallaTurnosTool.Mensajes.DiaEnBorrador,
            string.Format(ConsultarMallaTurnosTool.Mensajes.DiaTrabajo, "06:00-14:00", "Norte")));
        dias[3].Should().StartWith("2026-10-15 jue: 14:00-22:00 (Sur)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraDescansoMarcado_CuandoElDiaEnBorradorNoTieneBloques()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 0);

        dias[5].Should().Be("2026-10-17 sáb: " + string.Format(
            ConsultarMallaTurnosTool.Mensajes.DiaEnBorrador, ConsultarMallaTurnosTool.Mensajes.DiaDescanso));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraHorarioAbiertoConDuracionYMarca_CuandoElDiaEnBorradorEsDeHorarioAbierto()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 1);

        dias[0].Should().Be("2026-10-12 lun: " + string.Format(
            ConsultarMallaTurnosTool.Mensajes.DiaEnBorrador,
            string.Format(ConsultarMallaTurnosTool.Mensajes.DiaHorarioAbierto, "2 h", "Ocoa")));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_NoCambiaLaLinea_CuandoLaCasillaEsTrabajoDescansoOAusencia()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 0);

        dias[0].Should().StartWith("2026-10-12 lun: 06:00-14:00 (").And.NotContain("borrador");
        dias[1].Should().Be("2026-10-13 mar: descanso");
        dias[2].Should().Be("2026-10-14 mié: ausencia (Vacaciones)");
        dias[4].Should().Be("2026-10-16 vie: 14:00-22:00, descanso 18:00-18:30 (Norte)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_NoCambiaLosDiasSinBorrador_CuandoLaVistaNoLosTrae()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 1);

        dias.Skip(1).Should().OnlyContain(d => d.EndsWith("sin programar"));
    }

    [Fact]
    public async Task ConsultarMallaTurnos_AgregaMarcaDeVencido_CuandoElDiaEnBorradorVieneVencido()
    {
        var (tool, _, _) = Armar();

        var dias = await DiasDe(tool, 0);

        dias[3].Should().Be("2026-10-15 jue: " + string.Format(
            ConsultarMallaTurnosTool.Mensajes.DiaEnBorradorVencido,
            string.Format(ConsultarMallaTurnosTool.Mensajes.DiaTrabajo, "14:00-22:00", "Sur")));
        dias[5].Should().NotContain("vencido");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_LeeLaVistaUnaVezConCodigosSemanaYFechaDeReferencia_CuandoHayFilas()
    {
        var (tool, _, vista) = Armar();

        await DiasDe(tool, 0);

        vista.UltimaRequest!.Method.Method.Should().Be("QUERY");
        vista.UltimaRequest.RequestUri!.AbsolutePath.Should().Be("/api/programacion/borradores");
        var cuerpo = JsonNode.Parse(vista.UltimoCuerpoEnviado!)!.AsObject();
        cuerpo["colaboradores"]!.AsArray().Select(c => c!.GetValue<string>()).Should().Equal("C-102", "C-103");
        cuerpo["desde"]!.GetValue<string>().Should().Be("2026-10-12");
        cuerpo["hasta"]!.GetValue<string>().Should().Be("2026-10-18");
        cuerpo["fechaReferencia"]!.GetValue<string>().Should().Be("2026-10-07");
        cuerpo["take"].Should().BeNull("la composicion interna no pagina (CA-ADR-0038)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_NoLlamaALaVista_CuandoLaPaginaDeLaMallaNoTraeFilas()
    {
        var (tool, _, vista) = Armar(
            jsonMalla: """{"desde":"2026-10-12","hasta":"2026-10-18","elementos":[],"siguienteCursor":null}""");

        await tool.Run(null!, "2026-10-14", null, null, null, null, TestContext.Current.CancellationToken);

        vista.UltimaRequest.Should().BeNull("sin filas no hay a quien superponerle borrador");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_NoLlamaNiALaMallaNiALaVista_CuandoElGrupoQuedaVacio()
    {
        var (tool, malla, vista) = Armar();

        await tool.Run(null!, "2026-10-14", null, null, "C-999", null, TestContext.Current.CancellationToken);

        malla.UltimaRequest.Should().BeNull();
        vista.UltimaRequest.Should().BeNull();
    }

    [Fact]
    public async Task ConsultarMallaTurnos_TraduceElRechazo_CuandoFallaLaLecturaDelBorrador()
    {
        var (tool, _, _) = Armar("Rango invalido", HttpStatusCode.UnprocessableEntity);

        var resultado = await tool.Run(
            null!, "2026-10-14", null, null, null, null, TestContext.Current.CancellationToken);

        resultado.Should().Be(string.Format(ConsultarMallaTurnosTool.Mensajes.RechazoDelDominio, "Rango invalido"));
    }
}
