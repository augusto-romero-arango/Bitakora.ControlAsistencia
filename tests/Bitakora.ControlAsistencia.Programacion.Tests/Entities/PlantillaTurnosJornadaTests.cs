using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaTurnosJornadaTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001284");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static PlantillaTurnos CrearPlantilla() =>
        PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 8));

    private static PlantillaTurnos CrearPlantillaConJornada(Guid jornadaId, LimitesJornada limites, long version)
    {
        var plantilla = CrearPlantilla();
        plantilla.AsignarJornada(jornadaId, limites, version);
        return plantilla;
    }

    [Fact]
    public void AsignarJornada_RetornaAsignada_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = CrearPlantilla();

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        var evento = plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should()
            .ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.JornadaId.Should().Be(JornadaA);
        evento.Limites.Should().Be(Limites(42, 8));
        evento.VersionJornada.Should().Be(3);
        plantilla.JornadaId.Should().Be(JornadaA);
        plantilla.Limites.Should().Be(Limites(42, 8));
        plantilla.VersionJornada.Should().Be(3);
    }

    [Fact]
    public void AsignarJornada_RetornaAsignada_CuandoCambiaDeJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);

        var resultado = plantilla.AsignarJornada(JornadaB, Limites(40, 8), 1);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().HaveCount(2)
            .And.Subject.Last().JornadaId.Should().Be(JornadaB);
        plantilla.JornadaId.Should().Be(JornadaB);
        plantilla.VersionJornada.Should().Be(1);
    }

    [Fact]
    public void AsignarJornada_RetornaSinCambios_CuandoLaCopiaTieneLaMismaVersion()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().ContainSingle();
        plantilla.VersionJornada.Should().Be(3);
    }

    [Fact]
    public void AsignarJornada_RetornaSinCambios_CuandoLaCopiaEsMasNuevaQueLaSolicitada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 5);

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().ContainSingle();
        plantilla.VersionJornada.Should().Be(5);
    }

    [Fact]
    public void AsignarJornada_AutocorrigeLaCopia_CuandoLaCopiaEstaAtrasada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(40, 9), 4);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().HaveCount(2);
        plantilla.Limites.Should().Be(Limites(40, 9));
        plantilla.VersionJornada.Should().Be(4);
    }

    [Fact]
    public void AsignarJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = CrearPlantilla();
        plantilla.Retirar();

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().BeEmpty();
        plantilla.JornadaId.Should().BeNull();
    }

    [Fact]
    public void QuitarJornada_RetornaQuitada_CuandoLaPlantillaTieneJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.Quitada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosQuitada>().Should().ContainSingle()
            .Which.PlantillaId.Should().Be(PlantillaId);
        plantilla.JornadaId.Should().BeNull();
        plantilla.Limites.Should().BeNull();
        plantilla.VersionJornada.Should().Be(0);
    }

    [Fact]
    public void QuitarJornada_RetornaSinCambios_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = CrearPlantilla();

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosQuitada>().Should().BeEmpty();
        plantilla.JornadaId.Should().BeNull();
    }

    [Fact]
    public void QuitarJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);
        plantilla.Retirar();

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosQuitada>().Should().BeEmpty();
        plantilla.JornadaId.Should().Be(JornadaA);
    }

    [Fact]
    public void IniciarConJornada_EmiteCreadaAsignadaYUnaSolaAuditoriaEnEseOrden_CuandoNaceConJornada()
    {
        var creada = PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 8);

        var plantilla = PlantillaTurnos.IniciarConJornada(creada, JornadaA, Limites(42, 8), 3);

        plantilla.UncommittedEvents.Should().HaveCount(3);
        plantilla.UncommittedEvents[0].Should().Be(creada);
        plantilla.UncommittedEvents[1].Should().Be(
            JornadaDePlantillaDeTurnosAsignada.Crear(PlantillaId, JornadaA, Limites(42, 8), 3));
        var auditoria = plantilla.UncommittedEvents[2].Should()
            .BeOfType<AdvertenciasDePlantillaDeTurnosCalculadas>().Subject;
        auditoria.Advertencias.Should().Equal(
            Enumerable.Range(1, 8).Select(AdvertenciaPlantillaTurnos.DiaSinTurno));
        plantilla.JornadaId.Should().Be(JornadaA);
        plantilla.VersionJornada.Should().Be(3);
    }
}
