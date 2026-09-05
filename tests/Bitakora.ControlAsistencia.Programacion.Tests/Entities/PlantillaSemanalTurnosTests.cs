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

    [Fact]
    public void QuitarDia_RetornaQuitado_CuandoElSlotTieneTurnoAsignado()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        var resultado = plantilla.QuitarDia(1, DiaSemana.Lunes);

        resultado.Should().Be(ResultadoQuitarDia.Quitado);
        var evento = plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalQuitado>().Should()
            .ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Semana.Should().Be(1);
        evento.Dia.Should().BeSameAs(DiaSemana.Lunes);
    }

    // Observabilidad sin getters: el slot vacio se prueba porque AsignarDia vuelve a responder
    // Asignado con el mismo turno que tenia antes, en vez de SinCambios.
    [Fact]
    public void QuitarDia_DejaElSlotVacio_LuegoAsignarDiaVuelveARetornarAsignado()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);
        plantilla.QuitarDia(1, DiaSemana.Lunes);

        var resultado = plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        resultado.Should().Be(ResultadoAsignarDia.Asignado);
    }

    [Fact]
    public void QuitarDia_RetornaSinCambios_CuandoElDiaNuncaTuvoTurnoAsignado()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.QuitarDia(1, DiaSemana.Martes);

        resultado.Should().Be(ResultadoQuitarDia.SinCambios);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalQuitado>().Should().BeEmpty();
    }

    // El slot se localiza por (semana, dia) completo: quitar el mismo dia de otra semana no toca
    // el asignado -- AsignarDia con el mismo turno sigue respondiendo SinCambios.
    [Fact]
    public void QuitarDia_NoTocaElMismoDiaDeOtraSemana_CuandoLaSemanaPedidaEstaVacia()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);

        var resultado = plantilla.QuitarDia(2, DiaSemana.Lunes);

        resultado.Should().Be(ResultadoQuitarDia.SinCambios);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id).Should().Be(ResultadoAsignarDia.SinCambios);
    }

    [Fact]
    public void QuitarDia_RetornaSinCambios_CuandoElDiaYaFueQuitado()
    {
        var plantilla = CrearPlantilla(2);
        plantilla.AsignarDia(1, DiaSemana.Lunes, Turno1Id);
        plantilla.QuitarDia(1, DiaSemana.Lunes);

        var resultado = plantilla.QuitarDia(1, DiaSemana.Lunes);

        resultado.Should().Be(ResultadoQuitarDia.SinCambios);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalQuitado>().Should().ContainSingle();
    }

    // La semana se valida antes que el estado del dia, aunque ese dia ya este vacio.
    [Fact]
    public void QuitarDia_RetornaSemanaFueraDeRango_CuandoLaSemanaSuperaElTotalDeLaPlantilla()
    {
        var plantilla = CrearPlantilla(2);

        var resultado = plantilla.QuitarDia(3, DiaSemana.Lunes);

        resultado.Should().Be(ResultadoQuitarDia.SemanaFueraDeRango);
        plantilla.UncommittedEvents.OfType<DiaDePlantillaSemanalQuitado>().Should().BeEmpty();
    }
}
