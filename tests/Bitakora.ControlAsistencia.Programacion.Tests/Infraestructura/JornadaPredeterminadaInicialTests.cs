using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Infraestructura;

public class JornadaPredeterminadaInicialTests
{
    [Fact]
    public void Limites_SonCuarentaYDosHorasSemanalesConTopeDeOchoSinMinimoYUnDescanso()
    {
        var esperado = LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1);

        JornadaPredeterminadaInicial.Limites.Should().Be(esperado);
    }
}
