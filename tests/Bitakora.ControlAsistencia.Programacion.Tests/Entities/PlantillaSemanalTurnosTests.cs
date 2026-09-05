// Issue #621 CA-3/CA-4: PlantillaSemanalTurnos.AsignarDia -- precedencia semana fuera de rango >
// sin cambios (idempotencia) > asignado. Iniciar()/AsignarDia() son internal (ADR-0015);
// accesibles en este proyecto de tests via InternalsVisibleTo (Bitakora.ControlAsistencia.Programacion.csproj).

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

    // CA-3: primer PUT sobre un slot vacio -- Asignado, con el evento en no confirmados.
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

    // CA-3: reemplazo -- el segundo PUT sobre el mismo slot con OTRO turno tambien es Asignado.
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

    // CA-3: repetir el MISMO turno en el mismo slot es idempotente -- SinCambios, sin evento nuevo.
    [Fact]
    public void AsignarDia_RetornaSinCambios_CuandoElMismoTurnoYaEstaAsignadoAEseDia()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno2Id);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, Turno2Id);

        resultado.Should().Be(ResultadoAsignarDia.SinCambios);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().ContainSingle();
    }

    // CA-3: el mismo turno puede asignarse a OTRO slot sin conflicto (no hay unicidad de turno).
    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoElMismoTurnoSeAsignaAOtroSlot()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        var resultado = plantilla.AsignarDia(2, DiaSemana.Domingo, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().HaveCount(2);
    }

    // CA-4: semana fuera de rango -- sin evento nuevo.
    [Fact]
    public void AsignarDia_RetornaSemanaFueraDeRango_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.AsignarDia(3, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.SemanaFueraDeRango);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().BeEmpty();
    }

    // CA-4: borde inclusive -- la ultima semana de la plantilla SI es asignable.
    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoLaSemanaEsElBordeInclusiveDeLaPlantilla()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.AsignarDia(2, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalAsignado>().Should().ContainSingle();
    }
}
