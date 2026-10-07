using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.ObtenerJornada;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class JornadaDescribirTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000873");

    private static Jornada Crear(int topeHoras, int topeMinutos) =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(topeHoras, topeMinutos),
            HorasYMinutos.Crear(0, 0), 1)));

    [Fact]
    public void Describir_RetornaLosLimitesYLaDescripcion_CuandoLaJornadaFueCreada()
    {
        Crear(8, 0).Describir().Should().Be(new JornadaRespuesta(
            JornadaId,
            new HorasYMinutosRespuesta(42, 0),
            new HorasYMinutosRespuesta(8, 0),
            new HorasYMinutosRespuesta(0, 0),
            1,
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana"));
    }

    [Fact]
    public void Describir_RetornaMinutosDelTope_CuandoElTopeTraeMinutos()
    {
        Crear(8, 12).Describir().TopeDiario.Should().Be(new HorasYMinutosRespuesta(8, 12));
    }

    [Fact]
    public void Describir_MarcaPredeterminada_CuandoPreferenciasApuntanAEstaJornada()
    {
        Crear(8, 0).Describir(JornadaId).EsPredeterminada.Should().BeTrue();
    }

    [Fact]
    public void Describir_NoMarcaPredeterminada_CuandoPreferenciasApuntanAOtraJornada()
    {
        Crear(8, 0).Describir(Guid.Parse("019600a0-0000-7000-8000-000000000900")).EsPredeterminada.Should().BeFalse();
    }

    [Fact]
    public void Describir_NoMarcaPredeterminada_CuandoNoHayPreferencias()
    {
        Crear(8, 0).Describir(null).EsPredeterminada.Should().BeFalse();
    }
}
