using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.ReadModels.Programacion;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class CatalogoLimitesJornadaTests
{
    private static readonly Guid JornadaA = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a70");
    private static readonly Guid Predeterminada = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a71");

    private static LimitesDeJornada Vista(Guid id, int semanalesEnMinutos) =>
        new(id.ToString(), semanalesEnMinutos, 8 * 60, 0, 1, "irrelevante");

    private static LimitesJornada Limites(int horas, int minutos) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(horas, minutos), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    [Fact]
    public void Componer_ReconstruyeLimitesJornadaDesdeLosMinutosDeLaVista()
    {
        var catalogo = CatalogoLimitesJornada.Componer([Vista(JornadaA, 8 * 60 + 12)], null);

        catalogo.Should().ContainSingle()
            .Which.Should().Be(new JornadaDelCatalogo(JornadaA, Limites(8, 12)));
    }

    [Fact]
    public void Componer_IncluyeLaPredeterminada_CuandoLaVistaAunNoLaTiene()
    {
        var catalogo = CatalogoLimitesJornada.Componer([Vista(JornadaA, 40 * 60)], Vista(Predeterminada, 42 * 60));

        catalogo.Select(j => j.JornadaId).Should().BeEquivalentTo([JornadaA, Predeterminada]);
        catalogo.Single(j => j.JornadaId == Predeterminada).Limites.Should().Be(Limites(42, 0));
    }

    [Fact]
    public void Componer_IncluyeLaPredeterminadaUnaSolaVezConLosLimitesDeSuDocumento_CuandoLaVistaYaLaTiene()
    {
        var catalogo = CatalogoLimitesJornada.Componer(
            [Vista(JornadaA, 40 * 60), Vista(Predeterminada, 44 * 60)], Vista(Predeterminada, 42 * 60));

        catalogo.Should().ContainSingle(j => j.JornadaId == Predeterminada)
            .Which.Limites.Should().Be(Limites(44, 0));
    }
}
