using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class AuditoriaPlantillaTurnosTests
{
    private static readonly Turno Descanso = Turno.Crear("Libre", true, []);
    private static readonly Turno Incompleto = Turno.Crear("Sin franjas", false, []);

    private static Turno Trabajo(int horas) =>
        Turno.Crear("Turno", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(6 + horas, 0))]);

    // Tope diario 8 h, minimo diario 4 h.
    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(4, 0), 1);

    [Fact]
    public void Auditar_RetornaSoloPlantillaSinJornada_CuandoNoHayLimites()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            5, [new DiaDePlantillaTurnosAuditado(1, Trabajo(8))], null);

        resultado.Should().Equal(AdvertenciaPlantillaTurnos.PlantillaSinJornada());
    }

    [Fact]
    public void Auditar_RetornaDiaSinTurnoPorCadaDia_CuandoLaPlantillaEstaVacia()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(3, [], Limites());

        resultado.Should().Equal(
            AdvertenciaPlantillaTurnos.DiaSinTurno(1),
            AdvertenciaPlantillaTurnos.DiaSinTurno(2),
            AdvertenciaPlantillaTurnos.DiaSinTurno(3));
    }

    [Fact]
    public void Auditar_RetornaDiaSinTurno_CuandoElTurnoDelDiaEstaIncompleto()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            2, [new DiaDePlantillaTurnosAuditado(1, Incompleto), new DiaDePlantillaTurnosAuditado(2, Trabajo(6))],
            Limites());

        resultado.Should().Equal(AdvertenciaPlantillaTurnos.DiaSinTurno(1));
    }

    [Fact]
    public void Auditar_NoAdvierte_CuandoElDiaEsDescansoYLosDemasEstanDentroDeLimites()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            2, [new DiaDePlantillaTurnosAuditado(1, Descanso), new DiaDePlantillaTurnosAuditado(2, Trabajo(8))],
            Limites());

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Auditar_RetornaSuperaTopeDiario_CuandoElTurnoExcedeElTope()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            1, [new DiaDePlantillaTurnosAuditado(1, Trabajo(10))], Limites());

        resultado.Should().Equal(AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120));
    }

    [Fact]
    public void Auditar_RetornaPorDebajoDelMinimoDiario_CuandoElTurnoNoLlegaAlMinimo()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            1, [new DiaDePlantillaTurnosAuditado(1, Trabajo(3))], Limites());

        resultado.Should().Equal(AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(1, 60));
    }

    [Fact]
    public void Auditar_NoEmiteAdvertenciasSemanales_CuandoElCicloEsMultiploDeSiete()
    {
        var dias = Enumerable.Range(1, 14).Select(d => new DiaDePlantillaTurnosAuditado(d, Trabajo(8))).ToList();

        var resultado = AuditoriaPlantillaTurnos.Auditar(14, dias, Limites());

        resultado.Should().BeEmpty();
    }

    [Fact]
    public void Auditar_OrdenaPorDiaYLuegoPorTipo_CuandoHayVariasAdvertencias()
    {
        var resultado = AuditoriaPlantillaTurnos.Auditar(
            4,
            [
                new DiaDePlantillaTurnosAuditado(4, Trabajo(9)),
                new DiaDePlantillaTurnosAuditado(2, Trabajo(3)),
                new DiaDePlantillaTurnosAuditado(1, Trabajo(8))
            ],
            Limites());

        resultado.Should().Equal(
            AdvertenciaPlantillaTurnos.PorDebajoDelMinimoDiario(2, 60),
            AdvertenciaPlantillaTurnos.DiaSinTurno(3),
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(4, 60));
    }
}
