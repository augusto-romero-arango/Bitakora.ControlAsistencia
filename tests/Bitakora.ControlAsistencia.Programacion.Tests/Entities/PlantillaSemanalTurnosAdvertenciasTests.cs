using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantilla;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaSemanalTurnosAdvertenciasTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000889");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000701");

    private static readonly Turno Descanso = Turno.Crear("Libre", true, []);

    private static Turno Trabajo(int horas) =>
        Turno.Crear("Turno", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(6 + horas, 0))]);

    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static PlantillaSemanalTurnos Plantilla(int semanas = 2) =>
        PlantillaSemanalTurnos.Iniciar(PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", semanas));

    private static PlantillaSemanalTurnos PlantillaConJornada(int semanales, int semanas = 2)
    {
        var plantilla = Plantilla(semanas);
        plantilla.AsignarJornada(JornadaA, Limites(semanales), 1);
        return plantilla;
    }

    private static List<AdvertenciasDePlantillaSemanalCalculadas> Calculadas(PlantillaSemanalTurnos plantilla) =>
        plantilla.UncommittedEvents.OfType<AdvertenciasDePlantillaSemanalCalculadas>().ToList();

    [Fact]
    public void Iniciar_EmiteCreadaYLuegoAdvertenciasConPlantillaSinJornada_CuandoNaceSinJornada()
    {
        var plantilla = Plantilla();

        plantilla.UncommittedEvents.Should().HaveCount(2);
        plantilla.UncommittedEvents[0].Should().BeOfType<PlantillaSemanalCreada>();
        var evento = plantilla.UncommittedEvents[1].Should()
            .BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Advertencias.Should().Equal(SoloSinJornada);
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarDia_NoEmiteAdvertencias_CuandoNoCambianPorqueNoHayJornada()
    {
        var plantilla = Plantilla();

        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        plantilla.UncommittedEvents.Select(e => e.GetType()).Should().Equal(
            typeof(PlantillaSemanalCreada), typeof(AdvertenciasDePlantillaSemanalCalculadas),
            typeof(DiaDePlantillaSemanalAsignado));
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarDia_EmiteDiaAsignadoYLuegoAdvertencias_CuandoCambianLasAdvertencias()
    {
        var plantilla = PlantillaConJornada(42);

        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        var ultimos = plantilla.UncommittedEvents.TakeLast(2).ToList();
        ultimos[0].Should().BeOfType<DiaDePlantillaSemanalAsignado>();
        var evento = ultimos[1].Should().BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which;
        // Semana 1: solo el lunes de 8 h (480 de 2 520 min semanales).
        var esperadas = new[]
            {
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 2520 - 480),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1)
            }
            .Concat(Enumerable.Range(2, 6).Select(d => AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Desde(d))))
            .Concat(SemanaVacia(2, 42))
            .ToArray();
        evento.Advertencias.Should().Equal(esperadas);
        plantilla.Advertencias.Should().Equal(esperadas);
    }

    [Fact]
    public void AsignarDia_NoEmiteAdvertencias_CuandoEsNoOp()
    {
        var plantilla = PlantillaConJornada(42);
        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);
        var antes = plantilla.UncommittedEvents.Count;

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        resultado.Should().Be(ResultadoAsignarDia.SinCambios);
        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void AsignarDia_ReauditaYEmite_CuandoEsAutocorreccionDeLaCopia()
    {
        var plantilla = PlantillaConJornada(42, semanas: 1);
        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(6), 2);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        var ultimo = plantilla.UncommittedEvents.Last().Should()
            .BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which;
        ultimo.Advertencias.Should().Contain(AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 2520 - 360));
    }

    [Fact]
    public void QuitarDia_EmiteDiaQuitadoYLuegoAdvertencias_CuandoCambianLasAdvertencias()
    {
        var plantilla = PlantillaConJornada(42, semanas: 1);
        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        plantilla.QuitarDia(1, DiaSemana.Lunes);

        var ultimos = plantilla.UncommittedEvents.TakeLast(2).ToList();
        ultimos[0].Should().BeOfType<DiaDePlantillaSemanalQuitado>();
        ultimos[1].Should().BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which
            .Advertencias.Should().Equal(PlantillaVaciaConJornada(1, 42));
    }

    [Fact]
    public void QuitarDia_NoEmiteAdvertencias_CuandoElDiaYaEstaVacio()
    {
        var plantilla = PlantillaConJornada(42);
        var antes = plantilla.UncommittedEvents.Count;

        var resultado = plantilla.QuitarDia(1, DiaSemana.Lunes);

        resultado.Should().Be(ResultadoQuitarDia.SinCambios);
        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void AsignarJornada_PasaDeSinJornadaALosControlesCompletos_CuandoSeAsociaLaJornada()
    {
        var plantilla = Plantilla();

        plantilla.AsignarJornada(JornadaA, Limites(42), 1);

        var ultimos = plantilla.UncommittedEvents.TakeLast(2).ToList();
        ultimos[0].Should().BeOfType<JornadaDePlantillaSemanalAsignada>();
        ultimos[1].Should().BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which
            .Advertencias.Should().Equal(PlantillaVaciaConJornada(2, 42));
        plantilla.Advertencias.Should().Equal(PlantillaVaciaConJornada(2, 42));
    }

    [Fact]
    public void AsignarJornada_ReauditaConLosNuevosLimites_CuandoCambiaDeJornada()
    {
        var plantilla = PlantillaConJornada(42);

        plantilla.AsignarJornada(JornadaB, Limites(36), 1);

        plantilla.UncommittedEvents.Last().Should().BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which
            .Advertencias.Should().Equal(PlantillaVaciaConJornada(2, 36));
    }

    [Fact]
    public void AsignarJornada_NoEmiteAdvertencias_CuandoEsNoOp()
    {
        var plantilla = PlantillaConJornada(42);
        var antes = plantilla.UncommittedEvents.Count;

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42), 1);

        resultado.Should().Be(ResultadoAsignarJornada.SinCambios);
        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void QuitarJornada_VuelveASinJornada_CuandoLaPlantillaTeniaJornada()
    {
        var plantilla = PlantillaConJornada(42);

        plantilla.QuitarJornada();

        var ultimos = plantilla.UncommittedEvents.TakeLast(2).ToList();
        ultimos[0].Should().BeOfType<JornadaDePlantillaSemanalQuitada>();
        ultimos[1].Should().BeOfType<AdvertenciasDePlantillaSemanalCalculadas>().Which
            .Advertencias.Should().Equal(SoloSinJornada);
        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void QuitarJornada_NoEmiteAdvertencias_CuandoNoHayJornada()
    {
        var plantilla = Plantilla();
        var antes = plantilla.UncommittedEvents.Count;

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.SinCambios);
        plantilla.UncommittedEvents.Should().HaveCount(antes);
    }

    [Fact]
    public void Operaciones_NoEmitenAdvertencias_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = PlantillaConJornada(42);
        plantilla.Retirar();
        var antes = plantilla.UncommittedEvents.Count;

        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);
        plantilla.QuitarDia(1, DiaSemana.Lunes);
        plantilla.AsignarJornada(JornadaB, Limites(36), 1);
        plantilla.QuitarJornada();

        plantilla.UncommittedEvents.Should().HaveCount(antes);
        Calculadas(plantilla).Should().HaveCount(2);
    }

    [Fact]
    public void Retirar_NoEmiteAdvertencias_CuandoSeRetiraLaPlantilla()
    {
        var plantilla = PlantillaConJornada(42);
        var antes = Calculadas(plantilla).Count;

        plantilla.Retirar();

        Calculadas(plantilla).Should().HaveCount(antes);
        plantilla.UncommittedEvents.Last().Should().BeOfType<PlantillaSemanalRetirada>();
    }

    [Fact]
    public void Apply_RehidrataLasAdvertenciasVigentes_CuandoSeAplicaElEvento()
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla.Apply(PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", 2));

        plantilla.Apply(AdvertenciasDePlantillaSemanalCalculadas.Crear(PlantillaId, SoloSinJornada));

        plantilla.Advertencias.Should().Equal(SoloSinJornada);
    }

    [Fact]
    public void AsignarDia_NoReemiteAdvertencias_CuandoLaPlantillaRehidratadaYaLasTiene()
    {
        var plantilla = new PlantillaSemanalTurnos();
        plantilla.Apply(PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", 2));
        plantilla.Apply(AdvertenciasDePlantillaSemanalCalculadas.Crear(PlantillaId, SoloSinJornada));

        plantilla.AsignarDia(1, DiaSemana.Lunes, TurnoId, Trabajo(8), 1);

        plantilla.UncommittedEvents.Should().ContainSingle().Which.Should().BeOfType<DiaDePlantillaSemanalAsignado>();
    }
}
