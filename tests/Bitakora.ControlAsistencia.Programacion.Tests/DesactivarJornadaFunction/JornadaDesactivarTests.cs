using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DesactivarJornadaFunction;

public class JornadaDesactivarTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000869");

    private static LimitesJornada Limites(int tope) => LimitesJornada.Crear(
        HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(tope, 0), HorasYMinutos.Crear(0, 0), 1);

    private static Jornada Crear() => Jornada.Iniciar(JornadaCreada.Crear(JornadaId, Limites(8)));

    [Fact]
    public void Iniciar_DejaLaJornadaActiva_CuandoSeCrea()
    {
        var jornada = Crear();

        jornada.Activa.Should().BeTrue();
        jornada.Describir().Activa.Should().BeTrue();
    }

    [Fact]
    public void Desactivar_EmiteJornadaDesactivadaYDejaInactiva_CuandoEstaActiva()
    {
        var jornada = Crear();

        var resultado = jornada.Desactivar();

        resultado.Should().Be(ResultadoDesactivarJornada.Desactivada);
        jornada.UncommittedEvents.OfType<JornadaDesactivada>().Should().ContainSingle()
            .Which.JornadaId.Should().Be(JornadaId);
        jornada.Activa.Should().BeFalse();
        jornada.Describir().Activa.Should().BeFalse();
    }

    [Fact]
    public void Desactivar_RetornaYaInactivaSinEventos_CuandoYaEstaDesactivada()
    {
        var jornada = Crear();
        jornada.Desactivar();

        var resultado = jornada.Desactivar();

        resultado.Should().Be(ResultadoDesactivarJornada.YaInactiva);
        jornada.UncommittedEvents.OfType<JornadaDesactivada>().Should().ContainSingle();
        jornada.Activa.Should().BeFalse();
    }

    [Fact]
    public void ModificarLimites_ModificaSinReactivar_CuandoLaJornadaEstaDesactivada()
    {
        var jornada = Crear();
        jornada.Desactivar();

        var resultado = jornada.ModificarLimites(Limites(9));

        resultado.Should().Be(ResultadoModificarLimites.Modificados);
        jornada.Limites.Should().Be(Limites(9));
        jornada.Activa.Should().BeFalse();
    }
}
