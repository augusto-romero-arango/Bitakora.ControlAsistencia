using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class JornadaComoVistaTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000940");

    private static Jornada Crear() =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));

    [Fact]
    public void ComoVista_LlevaActivaTrue_CuandoLaJornadaEstaActiva()
    {
        Crear().ComoVista().Activa.Should().BeTrue();
    }

    [Fact]
    public void ComoVista_LlevaActivaFalse_CuandoLaJornadaFueDesactivada()
    {
        var jornada = Crear();
        jornada.Desactivar();

        jornada.ComoVista().Activa.Should().BeFalse();
    }
}
