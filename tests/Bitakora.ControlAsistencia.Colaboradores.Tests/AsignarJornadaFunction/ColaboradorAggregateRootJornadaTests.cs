using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction;

public class ColaboradorAggregateRootJornadaTests
{
    private static readonly Guid JornadaId = Guid.Parse("12345678-1234-4123-8123-123456789abc");

    [Fact]
    public void VinculacionIniciada_LimpiaJornada_CuandoHayReingreso()
    {
        var colaborador = new ColaboradorAggregateRoot();
        colaborador.Apply(new VinculacionIniciada("COL-001", new DateOnly(2026, 1, 15)));
        colaborador.Apply(new JornadaAsignada(JornadaId));
        colaborador.Apply(new VinculacionTerminada(new DateOnly(2026, 6, 1)));

        colaborador.Apply(new VinculacionIniciada("COL-002", new DateOnly(2026, 7, 1)));

        colaborador.JornadaId.Should().BeNull();
    }

    [Fact]
    public void AsignarJornada_RetornaSinCambios_CuandoJornadaYaEstaAsignada()
    {
        var colaborador = new ColaboradorAggregateRoot();
        colaborador.Apply(new VinculacionIniciada("COL-001", new DateOnly(2026, 1, 15)));
        colaborador.Apply(new JornadaAsignada(JornadaId));

        colaborador.AsignarJornada(JornadaId).Should().Be(ResultadoAsignacionJornada.SinCambios);
    }
}
