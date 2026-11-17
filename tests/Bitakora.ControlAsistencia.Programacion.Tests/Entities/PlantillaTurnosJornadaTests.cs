using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaTurnosJornadaTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001284");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    private static PlantillaTurnos CrearPlantilla() =>
        PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 8));

    private static PlantillaTurnos CrearPlantillaConJornada(Guid jornadaId)
    {
        var plantilla = CrearPlantilla();
        plantilla.AsignarJornada(jornadaId);
        return plantilla;
    }

    [Fact]
    public void AsignarJornada_RetornaAsignada_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = CrearPlantilla();

        var resultado = plantilla.AsignarJornada(JornadaA);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        var evento = plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should()
            .ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.JornadaId.Should().Be(JornadaA);
        plantilla.JornadaId.Should().Be(JornadaA);
    }

    [Fact]
    public void AsignarJornada_RetornaAsignada_CuandoCambiaDeJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA);

        var resultado = plantilla.AsignarJornada(JornadaB);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().HaveCount(2)
            .And.Subject.Last().JornadaId.Should().Be(JornadaB);
        plantilla.JornadaId.Should().Be(JornadaB);
    }

    [Fact]
    public void AsignarJornada_RetornaSinCambios_CuandoYaTieneEsaJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA);

        var resultado = plantilla.AsignarJornada(JornadaA);

        resultado.Should().Be(ResultadoAsignarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().ContainSingle();
        plantilla.JornadaId.Should().Be(JornadaA);
    }

    [Fact]
    public void AsignarJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = CrearPlantilla();
        plantilla.Retirar();

        var resultado = plantilla.AsignarJornada(JornadaA);

        resultado.Should().Be(ResultadoAsignarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosAsignada>().Should().BeEmpty();
        plantilla.JornadaId.Should().BeNull();
    }

    [Fact]
    public void QuitarJornada_RetornaQuitada_CuandoLaPlantillaTieneJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA);

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.Quitada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosQuitada>().Should().ContainSingle()
            .Which.PlantillaId.Should().Be(PlantillaId);
        plantilla.JornadaId.Should().BeNull();
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
        var plantilla = CrearPlantillaConJornada(JornadaA);
        plantilla.Retirar();

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaDeTurnosQuitada>().Should().BeEmpty();
        plantilla.JornadaId.Should().Be(JornadaA);
    }

    [Fact]
    public void IniciarConJornada_EmiteCreadaYAsignadaEnEseOrden_CuandoNaceConJornada()
    {
        var creada = PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 8);

        var plantilla = PlantillaTurnos.IniciarConJornada(creada, JornadaA);

        plantilla.UncommittedEvents.Should().HaveCount(2);
        plantilla.UncommittedEvents[0].Should().Be(creada);
        plantilla.UncommittedEvents[1].Should().BeOfType<JornadaDePlantillaDeTurnosAsignada>()
            .Which.Should().BeEquivalentTo(JornadaDePlantillaDeTurnosAsignada.Crear(PlantillaId, JornadaA));
        plantilla.JornadaId.Should().Be(JornadaA);
    }
}
