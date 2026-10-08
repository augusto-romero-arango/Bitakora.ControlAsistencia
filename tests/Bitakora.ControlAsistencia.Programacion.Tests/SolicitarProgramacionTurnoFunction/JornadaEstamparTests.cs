using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class JornadaEstamparTests
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000862");

    // Issue #861 CA-1: la copia la entrega el aggregate (Tell-don't-Ask).
    [Fact]
    public void Estampar_DevuelveLaCopiaConIdYLimites_CuandoLaJornadaFueCreada()
    {
        var jornada = Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));

        var copia = jornada.Estampar();

        copia.Should().Be(new JornadaProgramada(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
    }

    [Fact]
    public void Estampar_DevuelveLosLimitesVigentes_CuandoLaJornadaFueModificada()
    {
        var jornada = Jornada.Iniciar(JornadaCreada.Crear(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 30), HorasYMinutos.Crear(4, 0), 1)));
        jornada.ModificarLimites(LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1));

        var copia = jornada.Estampar();

        copia.Should().Be(new JornadaProgramada(JornadaId, LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));
    }
}
