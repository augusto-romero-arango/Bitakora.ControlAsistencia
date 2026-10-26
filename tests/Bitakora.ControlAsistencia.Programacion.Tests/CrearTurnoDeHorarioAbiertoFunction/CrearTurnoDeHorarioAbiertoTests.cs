using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoDeHorarioAbiertoFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;

public class CrearTurnoDeHorarioAbiertoTests
{
    [Fact]
    public void ToDuracion_ConvierteHorasYMinutos_CuandoSonValidos()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "T", new HorasYMinutosSolicitadas(7, 30));

        comando.ToDuracion().Should().Be(HorasYMinutos.Crear(7, 30));
    }

    [Fact]
    public void ToDuracion_LanzaAggregateException_CuandoHorasSonNegativas()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "T", new HorasYMinutosSolicitadas(-1, 0));

        var act = () => comando.ToDuracion();

        act.Should().ThrowExactly<AggregateException>();
    }

    [Fact]
    public void ToDuracion_LanzaAggregateException_CuandoMinutosExcedenCincuentaYNueve()
    {
        var comando = new CrearTurnoDeHorarioAbierto(Guid.NewGuid(), "T", new HorasYMinutosSolicitadas(1, 60));

        var act = () => comando.ToDuracion();

        act.Should().ThrowExactly<AggregateException>();
    }
}
