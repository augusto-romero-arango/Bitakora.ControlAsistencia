using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaTurnosSincronizarLimitesTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001296");
    private static readonly Guid JornadaA = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid JornadaB = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static PlantillaTurnos PlantillaConJornada(long version)
    {
        var plantilla = PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 4));
        plantilla.AsignarJornada(JornadaA, Limites(40, 8), version);
        return plantilla;
    }

    [Fact]
    public void SincronizarLimitesDeJornada_RetornaSincronizado_CuandoLaVersionEsMayor()
    {
        var plantilla = PlantillaConJornada(1);

        var resultado = plantilla.SincronizarLimitesDeJornada(JornadaA, Limites(44, 9), 2);

        resultado.Should().Be(ResultadoSincronizarLimitesDeJornada.Sincronizado);
        var evento = plantilla.UncommittedEvents.OfType<LimitesDeJornadaDePlantillaDeTurnosSincronizados>()
            .Should().ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.JornadaId.Should().Be(JornadaA);
        evento.Limites.Should().Be(Limites(44, 9));
        evento.VersionJornada.Should().Be(2);
        plantilla.Limites.Should().Be(Limites(44, 9));
        plantilla.VersionJornada.Should().Be(2);
    }

    [Fact]
    public void SincronizarLimitesDeJornada_RetornaSinCambios_CuandoLaVersionEsIgualOMenor()
    {
        var plantilla = PlantillaConJornada(3);
        var antes = plantilla.UncommittedEvents.Count;

        var igual = plantilla.SincronizarLimitesDeJornada(JornadaA, Limites(44, 9), 3);
        var menor = plantilla.SincronizarLimitesDeJornada(JornadaA, Limites(44, 9), 2);

        igual.Should().Be(ResultadoSincronizarLimitesDeJornada.SinCambios);
        menor.Should().Be(ResultadoSincronizarLimitesDeJornada.SinCambios);
        plantilla.UncommittedEvents.Should().HaveCount(antes);
        plantilla.Limites.Should().Be(Limites(40, 8));
    }

    [Fact]
    public void SincronizarLimitesDeJornada_RetornaSinCambios_CuandoLaPlantillaTieneOtraJornada()
    {
        var plantilla = PlantillaConJornada(1);

        var resultado = plantilla.SincronizarLimitesDeJornada(JornadaB, Limites(44, 9), 9);

        resultado.Should().Be(ResultadoSincronizarLimitesDeJornada.SinCambios);
        plantilla.VersionJornada.Should().Be(1);
    }

    [Fact]
    public void SincronizarLimitesDeJornada_RetornaSinCambios_CuandoLaPlantillaNoTieneJornada()
    {
        var plantilla = PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo Cocina", 4));

        var resultado = plantilla.SincronizarLimitesDeJornada(JornadaA, Limites(44, 9), 1);

        resultado.Should().Be(ResultadoSincronizarLimitesDeJornada.SinCambios);
        plantilla.Limites.Should().BeNull();
    }

    [Fact]
    public void SincronizarLimitesDeJornada_RetornaPlantillaRetirada_CuandoLaPlantillaEstaRetirada()
    {
        var plantilla = PlantillaConJornada(1);
        plantilla.Retirar();

        var resultado = plantilla.SincronizarLimitesDeJornada(JornadaA, Limites(44, 9), 2);

        resultado.Should().Be(ResultadoSincronizarLimitesDeJornada.PlantillaRetirada);
        plantilla.UncommittedEvents.OfType<LimitesDeJornadaDePlantillaDeTurnosSincronizados>().Should().BeEmpty();
    }
}
