using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaSemanalTurnosJornadaTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000867");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static PlantillaSemanalTurnos CrearPlantilla() =>
        PlantillaSemanalTurnos.Iniciar(PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", 2));

    private static PlantillaSemanalTurnos CrearPlantillaConJornada(Guid jornadaId, LimitesJornada limites, long version)
    {
        var plantilla = CrearPlantilla();
        plantilla.AsignarJornada(jornadaId, limites, version);
        return plantilla;
    }

    private static PlantillaSemanalTurnos RetirarPlantilla(PlantillaSemanalTurnos plantilla)
    {
        plantilla.Retirar();
        return plantilla;
    }

    [Fact]
    public void AsignarJornada_RetornaAsignada_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = CrearPlantilla();

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        var evento = plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should()
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
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should().HaveCount(2)
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
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should().ContainSingle();
        plantilla.VersionJornada.Should().Be(3);
    }

    [Fact]
    public void AsignarJornada_RetornaSinCambios_CuandoLaCopiaEsMasNuevaQueLaOfrecida()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(44, 8), 5);

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should().ContainSingle();
        plantilla.Limites.Should().Be(Limites(44, 8));
    }

    [Fact]
    public void AsignarJornada_RetornaAsignadaConLaCopiaNueva_CuandoLaCopiaEstaAtrasada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 5);

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(44, 9), 6);

        resultado.Should().Be(ResultadoAsignarJornada.Asignada);
        var ultimo = plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Last();
        ultimo.Limites.Should().Be(Limites(44, 9));
        ultimo.VersionJornada.Should().Be(6);
        plantilla.Limites.Should().Be(Limites(44, 9));
        plantilla.VersionJornada.Should().Be(6);
    }

    [Fact]
    public void AsignarJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = RetirarPlantilla(CrearPlantilla());

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 1);

        resultado.Should().Be(ResultadoAsignarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should().BeEmpty();
        plantilla.JornadaId.Should().BeNull();
    }

    [Fact]
    public void AsignarJornada_RetornaPlantillaRetirada_CuandoEstaRetiradaAunqueLaCopiaEsteAlDia()
    {
        var plantilla = RetirarPlantilla(CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3));

        var resultado = plantilla.AsignarJornada(JornadaA, Limites(42, 8), 3);

        resultado.Should().Be(ResultadoAsignarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalAsignada>().Should().ContainSingle();
    }

    [Fact]
    public void QuitarJornada_RetornaQuitada_CuandoLaPlantillaTieneJornada()
    {
        var plantilla = CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3);

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.Quitada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalQuitada>().Should().ContainSingle()
            .Which.PlantillaId.Should().Be(PlantillaId);
        plantilla.JornadaId.Should().BeNull();
        plantilla.Limites.Should().BeNull();
    }

    [Fact]
    public void QuitarJornada_RetornaSinCambios_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = CrearPlantilla();

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.SinCambios);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalQuitada>().Should().BeEmpty();
        plantilla.JornadaId.Should().BeNull();
    }

    [Fact]
    public void QuitarJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = RetirarPlantilla(CrearPlantillaConJornada(JornadaA, Limites(42, 8), 3));

        var resultado = plantilla.QuitarJornada();

        resultado.Should().Be(ResultadoQuitarJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<JornadaDePlantillaSemanalQuitada>().Should().BeEmpty();
        plantilla.JornadaId.Should().Be(JornadaA);
    }
}
