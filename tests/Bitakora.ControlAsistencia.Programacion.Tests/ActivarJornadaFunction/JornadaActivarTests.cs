using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ActivarJornadaFunction;

public class JornadaActivarTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000939");

    private static Jornada Crear() => Jornada.Iniciar(JornadaCreada.Crear(JornadaId,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1)));

    [Fact]
    public void Activar_EmiteJornadaActivadaYDejaActiva_CuandoEstaDesactivada()
    {
        var jornada = Crear();
        jornada.Desactivar();

        var resultado = jornada.Activar();

        resultado.Should().Be(ResultadoActivarJornada.Activada);
        jornada.UncommittedEvents.OfType<JornadaActivada>().Should().ContainSingle()
            .Which.JornadaId.Should().Be(JornadaId);
        jornada.Activa.Should().BeTrue();
        jornada.Describir().Activa.Should().BeTrue();
    }

    [Fact]
    public void Activar_RetornaYaActivaSinEventos_CuandoRecienCreada()
    {
        var jornada = Crear();

        var resultado = jornada.Activar();

        resultado.Should().Be(ResultadoActivarJornada.YaActiva);
        jornada.UncommittedEvents.OfType<JornadaActivada>().Should().BeEmpty();
        jornada.Activa.Should().BeTrue();
    }

    [Fact]
    public void Activar_RetornaYaActivaSinEventos_CuandoYaFueReactivada()
    {
        var jornada = Crear();
        jornada.Desactivar();
        jornada.Activar();

        var resultado = jornada.Activar();

        resultado.Should().Be(ResultadoActivarJornada.YaActiva);
        jornada.UncommittedEvents.OfType<JornadaActivada>().Should().ContainSingle();
        jornada.Activa.Should().BeTrue();
    }
}
