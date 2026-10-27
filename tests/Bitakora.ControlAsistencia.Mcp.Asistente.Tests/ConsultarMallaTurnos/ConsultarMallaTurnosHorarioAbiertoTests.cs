using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarMallaTurnos;

public class ConsultarMallaTurnosHorarioAbiertoTests
{
    private static async Task<string[]> DiasDelPrimero()
    {
        var (clienteMalla, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarMallaTurnos", "malla-semana-horario-abierto.json"));
        var (clienteFichas, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarMallaTurnos", "fichas-grupo.json"));
        var tool = new ConsultarMallaTurnosTool(
            new ControlHorasApi(clienteMalla),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            new RelojFalso(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(-5))));

        var resultado = await tool.Run(
            null!, "2026-10-14", null, null, null, null, TestContext.Current.CancellationToken);

        return [.. JsonNode.Parse(resultado)!["colaboradores"]![0]!["dias"]!.AsArray().Select(d => d!.GetValue<string>())];
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraHorarioAbiertoSinHoraConSede_CuandoLaCasillaEsHorarioAbierto()
    {
        var dias = await DiasDelPrimero();

        dias[5].Should().Be("2026-10-17 sáb: Abierto 2:00 horario abierto (Ocoa)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MantieneElFormatoActual_CuandoLaCasillaEsTrabajoNormal()
    {
        var dias = await DiasDelPrimero();

        dias[0].Should().Be("2026-10-12 lun: Manana 06:00-14:00 (Norte)");
    }
}
