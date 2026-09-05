// Iniciar()/AsignarDia() son internal (ADR-0015): este proyecto de tests los alcanza via el
// InternalsVisibleTo de Bitakora.ControlAsistencia.Programacion.csproj.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaSemanalTurnosTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000000621");
    private static readonly Guid Turno1Id = Guid.Parse("019600a0-0000-7000-8000-000000000701");
    private static readonly Guid Turno2Id = Guid.Parse("019600a0-0000-7000-8000-000000000702");

    private static PlantillaSemanalTurnos CrearPlantilla(int semanas) =>
        PlantillaSemanalTurnos.Iniciar(PlantillaSemanalCreada.Crear(PlantillaId, "Semana Cocina", semanas));

    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoElSlotEstaVacio()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        var evento = plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should()
            .ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Semana.Should().Be(1);
        evento.Dia.Should().BeSameAs(DiaSemana.Lunes);
        evento.TurnoId.Should().Be(Turno1Id);
    }

    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoReemplazaElTurnoDeUnSlotYaOcupado()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, Turno2Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().HaveCount(2)
            .And.Subject.Last().TurnoId.Should().Be(Turno2Id);
    }

    [Fact]
    public void AsignarDia_RetornaSinCambios_CuandoElMismoTurnoYaEstaAsignadoAEseDia()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno2Id);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, Turno2Id);

        resultado.Should().Be(ResultadoAsignarDia.SinCambios);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().ContainSingle();
    }

    // Un turno no es exclusivo de un slot: puede repetirse en varios dias de la plantilla.
    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoElMismoTurnoSeAsignaAOtroSlot()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        var resultado = plantilla.AsignarDia(2, DiaSemana.Domingo, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().HaveCount(2);
    }

    [Fact]
    public void AsignarDia_RetornaSemanaFueraDeRango_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.AsignarDia(3, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.SemanaFueraDeRango);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().BeEmpty();
    }

    // El tope de semanas es inclusive: la ultima semana de la plantilla SI es asignable.
    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoLaSemanaEsElBordeInclusiveDeLaPlantilla()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.AsignarDia(2, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().ContainSingle();
    }
}
