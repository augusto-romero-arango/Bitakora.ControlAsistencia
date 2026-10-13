using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;
using Bitakora.ControlAsistencia.Mcp.Asistente.SolicitarProgramacionTurno;
using Bitakora.ControlAsistencia.Mcp.Asistente.Tests.Soporte;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.SolicitarProgramacionTurno;

public class JornadaDelCandidatoResolutoresTests
{
    private static readonly Guid J1 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private const string DirectorioJson = """
        [
          {"identificacion":"CC-1","nombreCompleto":"Ana","codigoColaborador":"A1","codigoSede":null,
           "vigenteDesde":"2025-01-01","vigenteHasta":null,"jornadaId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"},
          {"identificacion":"CC-2","nombreCompleto":"Beto","codigoColaborador":"B2","codigoSede":null,
           "vigenteDesde":"2025-01-01","vigenteHasta":null,"jornadaId":null},
          {"identificacion":"CC-3","nombreCompleto":"Cami","codigoColaborador":"C3","codigoSede":null,
           "vigenteDesde":"2025-01-01","vigenteHasta":null}
        ]
        """;

    private const string FichasJson = """
        [
          {"id":"CC-1","nombreCompleto":"Ana","codigoColaborador":"A1","vigenteDesde":"2025-01-01",
           "vigenteHasta":null,"etiquetas":[],"codigoSede":null,"jornadaId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"},
          {"id":"CC-2","nombreCompleto":"Beto","codigoColaborador":"B2","vigenteDesde":"2025-01-01",
           "vigenteHasta":null,"etiquetas":[],"codigoSede":null,"jornadaId":null},
          {"id":"CC-3","nombreCompleto":"Cami","codigoColaborador":"C3","vigenteDesde":"2025-01-01",
           "vigenteHasta":null,"etiquetas":[],"codigoSede":null}
        ]
        """;

    [Fact]
    public async Task ResolutorPorLista_CopiaLaJornadaDelDirectorio_CuandoLaTiene()
    {
        var (cliente, _) = ClienteFalso.Con(DirectorioJson);

        var resultado = await new ResolutorCandidatosPorLista(new ColaboradoresApi(cliente))
            .ResolverAsync(["CC-1", "CC-2", "CC-3"], TestContext.Current.CancellationToken);

        resultado.Candidatos.Single(c => c.Identificacion == "CC-1").JornadaId.Should().Be(J1);
        resultado.Candidatos.Single(c => c.Identificacion == "CC-2").JornadaId.Should().BeNull();
        resultado.Candidatos.Single(c => c.Identificacion == "CC-3").JornadaId.Should().BeNull();
    }

    [Fact]
    public async Task ResolutorPorGrupo_CopiaLaJornadaDeLasFichas_CuandoLaTiene()
    {
        var (cliente, _) = ClienteFalso.Con(FichasJson);

        var resultado = await new ResolutorCandidatosPorGrupo(new ColaboradoresApi(cliente))
            .ResolverAsync(new DateOnly(2026, 9, 1), null, [], TestContext.Current.CancellationToken);

        resultado.Candidatos.Single(c => c.Identificacion == "CC-1").JornadaId.Should().Be(J1);
        resultado.Candidatos.Single(c => c.Identificacion == "CC-2").JornadaId.Should().BeNull();
        resultado.Candidatos.Single(c => c.Identificacion == "CC-3").JornadaId.Should().BeNull();
    }
}
