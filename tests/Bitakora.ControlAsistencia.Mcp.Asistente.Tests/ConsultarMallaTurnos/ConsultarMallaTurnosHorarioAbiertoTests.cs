using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarMallaTurnos;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarMallaTurnos;

public class ConsultarMallaTurnosHorarioAbiertoTests
{
    private static async Task<string[]> DiasDelPrimero(string fixture = "malla-semana-horario-abierto.json")
    {
        var (clienteMalla, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarMallaTurnos", fixture));
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

        dias[5].Should().Be("2026-10-17 sáb: horario abierto de 2 h (Ocoa)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MantieneElFormatoActual_CuandoLaCasillaEsTrabajoNormal()
    {
        var dias = await DiasDelPrimero();

        dias[0].Should().Be("2026-10-12 lun: 06:00-14:00 (Norte)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_SumaSoloBloquesOrdinarios_CuandoElHorarioAbiertoTieneHorasYMinutos()
    {
        var dias = await DiasDelPrimero("malla-semana-duraciones.json");

        dias[0].Should().Be("2026-10-12 lun: horario abierto de 6 h 30 min (Ocoa)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraSoloMinutos_CuandoElHorarioAbiertoDuraMenosDeUnaHora()
    {
        var dias = await DiasDelPrimero("malla-semana-duraciones.json");

        dias[1].Should().Be("2026-10-13 mar: horario abierto de 30 min (Ocoa)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraElNombreDelTurno_CuandoElHorarioResumidoEstaVacio()
    {
        var dias = await DiasDelPrimero("malla-semana-duraciones.json");

        dias[2].Should().Be("2026-10-14 mié: Turno Antiguo (Norte)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_MuestraElNombreDelTurno_CuandoElHorarioResumidoEsNulo()
    {
        var dias = await DiasDelPrimero("malla-semana-duraciones.json");

        dias[3].Should().Be("2026-10-15 jue: Turno Viejo (Norte)");
    }

    [Fact]
    public async Task ConsultarMallaTurnos_ConservaDescansoAusenciaYSinProgramar_CuandoLaCasillaNoEsTrabajo()
    {
        var dias = await DiasDelPrimero("malla-semana-duraciones.json");

        dias[4].Should().Be("2026-10-16 vie: descanso");
        dias[5].Should().Be("2026-10-17 sáb: ausencia (Vacaciones)");
        dias[6].Should().Be("2026-10-18 dom: sin programar");
    }
}
