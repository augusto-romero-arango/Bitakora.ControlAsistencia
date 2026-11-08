using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ObtenerJornada;

public class JornadaDescansoAcumuladoDescribirTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001437");

    private static Jornada Crear() =>
        Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));

    [Fact]
    public void Describir_RetornaDescansoAcumuladoFalsoYDescripcionDeHoy_CuandoNoEstaMarcada()
    {
        var respuesta = Crear().Describir();

        respuesta.DescansoAcumulado.Should().BeFalse();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }

    [Fact]
    public void Describir_IncluyeLaMarcaEnLaDescripcion_CuandoEstaMarcadaSinModalidad()
    {
        var jornada = Crear();
        jornada.Apply(DescansoAcumuladoDeJornadaMarcado.Crear(JornadaId));

        var respuesta = jornada.Describir();

        respuesta.DescansoAcumulado.Should().BeTrue();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, con descanso acumulado");
    }

    [Fact]
    public void Describir_IncluyeModalidadYMarca_CuandoTieneAmbas()
    {
        var jornada = Crear();
        jornada.Apply(ModalidadDeJornadaAsignada.Crear(JornadaId, ModalidadJornada.SinSolucionDeContinuidad));
        jornada.Apply(DescansoAcumuladoDeJornadaMarcado.Crear(JornadaId));

        var respuesta = jornada.Describir();

        respuesta.DescansoAcumulado.Should().BeTrue();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, modalidad Sin solución de continuidad, con descanso acumulado");
    }

    [Fact]
    public void Describir_VuelveALaDescripcionDeHoy_CuandoSeDesmarca()
    {
        var jornada = Crear();
        jornada.Apply(DescansoAcumuladoDeJornadaMarcado.Crear(JornadaId));
        jornada.Apply(DescansoAcumuladoDeJornadaDesmarcado.Crear(JornadaId));

        var respuesta = jornada.Describir();

        respuesta.DescansoAcumulado.Should().BeFalse();
        respuesta.Descripcion.Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }
}
