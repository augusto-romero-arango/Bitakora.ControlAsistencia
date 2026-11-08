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

    [Fact]
    public void ComoVista_LlevaModalidadNulaYDescripcionDeLimites_CuandoLaJornadaNoTieneModalidad()
    {
        var vista = Crear().ComoVista();

        vista.Modalidad.Should().BeNull();
        vista.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }

    [Fact]
    public void ComoVista_LlevaModalidadYDescripcionConModalidad_CuandoLaJornadaTieneModalidad()
    {
        var jornada = Crear();
        jornada.AsignarModalidad(ModalidadJornada.Flexible);

        var vista = jornada.ComoVista();

        vista.Modalidad.Should().Be("Flexible");
        vista.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, modalidad Flexible");
    }

    [Fact]
    public void ComoVista_LlevaDescansoAcumuladoFalseYDescripcionSinMarca_CuandoLaJornadaNoEstaMarcada()
    {
        var vista = Crear().ComoVista();

        vista.DescansoAcumulado.Should().BeFalse();
        vista.Descripcion.Should().NotContain("descanso acumulado");
    }

    [Fact]
    public void ComoVista_LlevaDescansoAcumuladoTrueYDescripcionConMarca_CuandoLaJornadaEstaMarcada()
    {
        var jornada = Crear();
        jornada.MarcarDescansoAcumulado();

        var vista = jornada.ComoVista();

        vista.DescansoAcumulado.Should().BeTrue();
        vista.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, con descanso acumulado");
    }
}
