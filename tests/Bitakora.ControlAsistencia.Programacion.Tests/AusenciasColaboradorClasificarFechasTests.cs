using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests;

public class AusenciasColaboradorClasificarFechasTests
{
    private static readonly DateOnly Dia1 = new(2026, 10, 1);
    private static readonly DateOnly Dia2 = new(2026, 10, 2);
    private static readonly DateOnly Dia3 = new(2026, 10, 3);
    private static readonly DateOnly Dia4 = new(2026, 10, 4);

    private static AusenciasColaborador ConVacaciones(DateOnly inicio, DateOnly fin) =>
        AusenciasColaborador.Iniciar(new AusenciaProgramada(
            Guid.NewGuid(), new ColaboradorProgramado("CC-1", "E001", "Ana"),
            inicio, fin, MotivoAusencia.Vacaciones));

    [Fact]
    public void ClasificarFechas_SeparaLibresDeCubiertas_CuandoUnaAusenciaCubreParteDeLasFechas()
    {
        var ausencias = ConVacaciones(Dia2, Dia3);

        var clasificacion = ausencias.ClasificarFechas([Dia1, Dia2, Dia3, Dia4]);

        clasificacion.Libres.Should().Equal(Dia1, Dia4);
        clasificacion.ConAusencia.Should().Equal(
            new FechaConAusencia(Dia2, MotivoAusencia.Vacaciones),
            new FechaConAusencia(Dia3, MotivoAusencia.Vacaciones));
    }

    [Fact]
    public void ClasificarFechas_DejaTodasLibres_CuandoNingunaFechaCaeEnLaAusencia()
    {
        var ausencias = ConVacaciones(Dia4, Dia4);

        var clasificacion = ausencias.ClasificarFechas([Dia1, Dia2]);

        clasificacion.Libres.Should().Equal(Dia1, Dia2);
        clasificacion.ConAusencia.Should().BeEmpty();
    }
}
