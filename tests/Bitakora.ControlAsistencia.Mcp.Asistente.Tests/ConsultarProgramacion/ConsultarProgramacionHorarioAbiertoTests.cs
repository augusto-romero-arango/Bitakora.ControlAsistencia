using System.Text.Json.Nodes;
using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.ConsultarProgramacion;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.ConsultarProgramacion;

public class ConsultarProgramacionHorarioAbiertoTests
{
    private static async Task<string[]> BloquesDe(string fixture)
    {
        var (clienteTurnos, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarProgramacion", fixture));
        var (clienteFichas, _) = ClienteFalso.Con("""{"elementos":[]}""");
        var (clienteSedes, _) = ClienteFalso.Con(Fixtures.Leer("ConsultarProgramacion", "ficha-sede.json"));
        var tool = new ConsultarProgramacionTool(
            new ControlHorasApi(clienteTurnos),
            new ResolutorCandidatosPorGrupo(new ColaboradoresApi(clienteFichas)),
            new ResolutorSedePorCodigo(new SedesApi(clienteSedes)));

        var resultado = await tool.Run(
            null!, "2026-11-07", "2026-11-07", null, null, null, null, null, TestContext.Current.CancellationToken);

        return [.. JsonNode.Parse(resultado)!["dias"]![0]!["bloques"]!.AsArray().Select(b => b!.GetValue<string>())];
    }

    [Fact]
    public async Task ConsultarProgramacion_MuestraHorarioAbiertoDeDosHorasConSede_CuandoElDiaEsHorarioAbierto()
    {
        var bloques = await BloquesDe("turnos-vigentes-abierto-2h.json");

        bloques.Should().Equal("horario abierto de 2 h, sede: Ocoa");
    }

    [Fact]
    public async Task ConsultarProgramacion_MuestraHorasYMinutos_CuandoElHorarioAbiertoDuraSeisHorasYMedia()
    {
        var bloques = await BloquesDe("turnos-vigentes-abierto-6h30.json");

        bloques.Should().Equal("horario abierto de 6 h 30 min, sede: Ocoa");
    }

    [Fact]
    public async Task ConsultarProgramacion_OmiteLaSede_CuandoElHorarioAbiertoNoLaTiene()
    {
        var bloques = await BloquesDe("turnos-vigentes-abierto-sin-sede.json");

        bloques.Should().Equal("horario abierto de 2 h");
    }

    [Fact]
    public async Task ConsultarProgramacion_SumaLaDuracionCompleta_CuandoElBloqueCruzaLaMedianoche()
    {
        var bloques = await BloquesDe("turnos-vigentes-abierto-20h-partido.json");

        bloques.Should().Equal("horario abierto de 20 h, sede: Ocoa");
    }

    [Fact]
    public async Task ConsultarProgramacion_SumaLaDuracionCompleta_CuandoLosBloquesLleganPartidosEnLaMedianoche()
    {
        var bloques = await BloquesDe("turnos-vigentes-abierto-20h-corte.json");

        bloques.Should().Equal("horario abierto de 20 h, sede: Ocoa");
    }
}
