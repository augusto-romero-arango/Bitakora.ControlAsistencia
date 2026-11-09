using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class JornadaModalidadDescribirTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001436");

    private static Jornada Crear() =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));

    [Fact]
    public void Describir_RetornaModalidadNulaYDescripcionDeHoy_CuandoLaJornadaNoTieneModalidad()
    {
        var respuesta = Crear().Describir();

        respuesta.Modalidad.Should().BeNull();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }

    [Fact]
    public void Describir_RetornaLaModalidadYLaDescripcionQueLaIncluye_CuandoSeAsignoModalidad()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.TurnosSucesivos));

        var respuesta = jornada.Describir();

        respuesta.Modalidad.Should().Be("TurnosSucesivos");
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, modalidad Turnos sucesivos");
    }

    [Fact]
    public void Describir_RetornaModalidadNulaYDescripcionDeHoy_CuandoSeQuitoLaModalidad()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.Flexible));
        jornada.Apply(ModalidadDeJornadaQuitada.Crear(JornadaId));

        var respuesta = jornada.Describir();

        respuesta.Modalidad.Should().BeNull();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }
}
