using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Eventos;

public class TurnoDePlantillaSemanalSincronizadoTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000882");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000883");

    [Fact]
    public void Crear_ExponeDatosRecibidos_ConTurnoVigente()
    {
        var turno = Turno.Crear("Descanso Compensatorio", true, []);

        var evento = TurnoDePlantillaSemanalSincronizado.Crear(PlantillaId, TurnoId, turno, 7, false);

        evento.PlantillaId.Should().Be(PlantillaId);
        evento.TurnoId.Should().Be(TurnoId);
        evento.Turno.Should().Be(turno);
        evento.VersionTurno.Should().Be(7);
        evento.Retirado.Should().BeFalse();
    }

    [Fact]
    public void Crear_MarcaRetirado_CuandoTurnoRetirado()
    {
        var turno = Turno.Crear("Descanso Compensatorio", true, []);

        var evento = TurnoDePlantillaSemanalSincronizado.Crear(PlantillaId, TurnoId, turno, 8, true);

        evento.Retirado.Should().BeTrue();
        evento.VersionTurno.Should().Be(8);
    }
}
