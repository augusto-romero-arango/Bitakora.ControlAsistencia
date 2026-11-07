using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ValueObjects;

public class DescripcionJornadaTests
{
    private static LimitesJornada Limites() => LimitesJornada.Crear(
        HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1);

    [Fact]
    public void Componer_RetornaLosLimitesSeguidosDeLaModalidad_CuandoTraeModalidad()
    {
        DescripcionJornada.Componer(Limites(), ModalidadJornada.TurnosSucesivos).Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana, modalidad Turnos sucesivos");
    }

    [Fact]
    public void Componer_RetornaSoloLosLimites_CuandoNoTraeModalidad()
    {
        DescripcionJornada.Componer(Limites(), null).Should().Be(
            "42 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
    }
}
