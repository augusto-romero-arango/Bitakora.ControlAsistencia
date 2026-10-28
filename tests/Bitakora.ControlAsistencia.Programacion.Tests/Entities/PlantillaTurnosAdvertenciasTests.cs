using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantillaTurnos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaTurnosAdvertenciasTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001292");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");
    private const int Dias = 8;

    private static Turno Trabajo(int horas) =>
        Turno.Crear("Turno", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(6 + horas, 0))]);

    private static LimitesJornada Limites(int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(36, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static PlantillaTurnos Plantilla() =>
        PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", Dias));

    private static PlantillaTurnos PlantillaConJornada(int tope = 8)
    {
        var plantilla = Plantilla();
        plantilla.AsignarJornada(JornadaA, Limites(tope), 1);
        return plantilla;
    }

    private static Type[] Tipos(PlantillaTurnos p) => p.UncommittedEvents.Select(e => e.GetType()).ToArray();

    [Fact]
    public void Iniciar_EmiteCreadaYLuegoAdvertenciasConPlantillaSinJornada()
    {
        var plantilla = Plantilla();

        Tipos(plantilla).Should().Equal(
            typeof(PlantillaDeTurnosCreada), typeof(AdvertenciasDePlantillaDeTurnosCalculadas));
        var evento = plantilla.UncommittedEvents[1].Should()
            .BeOfType<AdvertenciasDePlantillaDeTurnosCalculadas>().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Advertencias.Should().Equal(SoloSinJornada);
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarDia_EmiteSoloElDelComando_CuandoLasAdvertenciasNoCambianPorqueNoHayJornada()
    {
        var plantilla = Plantilla();

        plantilla.AsignarDia(1, TurnoId, Trabajo(8), 1);

        Tipos(plantilla).Should().Equal(
            typeof(PlantillaDeTurnosCreada), typeof(AdvertenciasDePlantillaDeTurnosCalculadas),
            typeof(DiaDePlantillaDeTurnosAsignado));
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarDia_EmiteDiaAsignadoYLuegoAdvertencias_CuandoCambianLasAdvertencias()
    {
        var plantilla = PlantillaConJornada();

        plantilla.AsignarDia(1, TurnoId, Trabajo(10), 1);

        var ultimos = plantilla.UncommittedEvents.TakeLast(2).ToList();
        ultimos[0].Should().BeOfType<DiaDePlantillaDeTurnosAsignado>();
        var evento = ultimos[1].Should().BeOfType<AdvertenciasDePlantillaDeTurnosCalculadas>().Which;
        var esperadas = new[] { AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120) }
            .Concat(Enumerable.Range(2, Dias - 1).Select(AdvertenciaPlantillaTurnos.DiaSinTurno))
            .ToArray();
        evento.Advertencias.Should().Equal(esperadas);
        plantilla.Advertencias.Should().Equal(esperadas);
    }

    [Fact]
    public void AsignarDia_NoEmiteAdvertencias_CuandoEsNoOp()
    {
        var plantilla = PlantillaConJornada();
        plantilla.AsignarDia(1, TurnoId, Trabajo(8), 1);
        var antes = plantilla.UncommittedEvents.Count;

        plantilla.AsignarDia(1, TurnoId, Trabajo(8), 1);

        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void AsignarJornada_PasaDeSinJornadaALosControlesDiarios()
    {
        var plantilla = Plantilla();

        plantilla.AsignarJornada(JornadaA, Limites(8), 1);

        Tipos(plantilla).TakeLast(2).Should().Equal(
            typeof(JornadaDePlantillaDeTurnosAsignada), typeof(AdvertenciasDePlantillaDeTurnosCalculadas));
        plantilla.Advertencias.Should().Equal(DiasSinTurno(Dias));
    }

    [Fact]
    public void AsignarJornada_ReAudita_CuandoCambiaAOtraJornadaConOtrosLimites()
    {
        var plantilla = PlantillaConJornada(8);
        plantilla.AsignarDia(1, TurnoId, Trabajo(8), 1);

        plantilla.AsignarJornada(JornadaB, Limites(6), 1);

        plantilla.UncommittedEvents.Last().Should().BeOfType<AdvertenciasDePlantillaDeTurnosCalculadas>()
            .Which.Advertencias.Should().Contain(AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120));
    }

    [Fact]
    public void QuitarJornada_VuelveAPlantillaSinJornada()
    {
        var plantilla = PlantillaConJornada();

        plantilla.QuitarJornada();

        Tipos(plantilla).TakeLast(2).Should().Equal(
            typeof(JornadaDePlantillaDeTurnosQuitada), typeof(AdvertenciasDePlantillaDeTurnosCalculadas));
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarJornada_NoEmiteAdvertencias_CuandoEsNoOp()
    {
        var plantilla = PlantillaConJornada();
        var antes = plantilla.UncommittedEvents.Count;

        plantilla.AsignarJornada(JornadaA, Limites(8), 1);

        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void QuitarJornada_NoEmiteAdvertencias_CuandoNoHayJornada()
    {
        var plantilla = Plantilla();
        var antes = plantilla.UncommittedEvents.Count;

        plantilla.QuitarJornada();

        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void Operaciones_NoEmitenAdvertencias_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = Plantilla();
        plantilla.Retirar();
        var antes = plantilla.UncommittedEvents.Count;

        plantilla.AsignarJornada(JornadaA, Limites(8), 1);
        plantilla.AsignarDia(1, TurnoId, Trabajo(8), 1);
        plantilla.QuitarJornada();

        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void Retirar_NoAudita()
    {
        var plantilla = PlantillaConJornada();

        plantilla.Retirar();

        plantilla.UncommittedEvents.Last().Should().BeOfType<PlantillaDeTurnosRetirada>();
    }
}
