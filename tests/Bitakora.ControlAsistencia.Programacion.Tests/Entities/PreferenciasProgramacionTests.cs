using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PreferenciasProgramacionTests
{
    private static readonly Guid JornadaId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b");

    [Fact]
    public void ComputarStreamId_ArmaPrefijoPpYTenant()
    {
        PreferenciasProgramacion.ComputarStreamId("tenant-x").Should().Be("pp:tenant-x");
    }

    [Fact]
    public void ComputarStreamId_SeDivideEnDosPartesPorDosPuntos()
    {
        PreferenciasProgramacion.ComputarStreamId("tenant-x").Split(':').Should().HaveCount(2);
    }

    [Fact]
    public void Iniciar_ExponeLaJornadaPredeterminadaAsignada()
    {
        var preferencias = PreferenciasProgramacion.Iniciar(new JornadaPredeterminadaAsignada(JornadaId));

        preferencias.JornadaPredeterminada().Should().Be(JornadaId);
    }

    [Fact]
    public void Apply_ReemplazaLaJornadaPredeterminada_CuandoLlegaOtraAsignacion()
    {
        var otra = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");
        var preferencias = PreferenciasProgramacion.Iniciar(new JornadaPredeterminadaAsignada(JornadaId));

        preferencias.Apply(new JornadaPredeterminadaAsignada(otra));

        preferencias.JornadaPredeterminada().Should().Be(otra);
    }
}
