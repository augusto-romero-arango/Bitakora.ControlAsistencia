using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.Entities;

public class PlantillaTurnosTests
{
    private static readonly Guid PlantillaId = Guid.Parse("019600a0-0000-7000-8000-000000001281");
    private static readonly Guid Turno1Id = Guid.Parse("019600a0-0000-7000-8000-000000000701");
    private static readonly Guid Turno2Id = Guid.Parse("019600a0-0000-7000-8000-000000000702");

    private static readonly Turno CopiaTurno =
        Turno.Crear("Turno Manana", false, [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno CopiaTurnoEditado = Turno.Crear(
        "Turno Manana", false,
        [
            FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0)),
            FranjaOrdinaria.Crear(new TimeOnly(15, 0), new TimeOnly(18, 0))
        ]);

    private static PlantillaTurnos CrearPlantilla(int diasDelCiclo) =>
        PlantillaTurnos.Iniciar(PlantillaDeTurnosCreada.Crear(PlantillaId, "Ciclo", diasDelCiclo));

    private static IEnumerable<DiaDePlantillaDeTurnosAsignado> Emitidos(PlantillaTurnos plantilla) =>
        plantilla.UncommittedEvents.OfType<DiaDePlantillaDeTurnosAsignado>();

    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoElDiaEstaVacio()
    {
        var plantilla = CrearPlantilla(8);

        var resultado = plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 1);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.Asignado);
        var evento = Emitidos(plantilla).Should().ContainSingle().Which;
        evento.PlantillaId.Should().Be(PlantillaId);
        evento.Dia.Should().Be(3);
        evento.TurnoId.Should().Be(Turno1Id);
        evento.Turno.Should().Be(CopiaTurno);
        evento.VersionTurno.Should().Be(1);
    }

    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoElDiaEsElUltimoDelCiclo()
    {
        var plantilla = CrearPlantilla(8);

        var resultado = plantilla.AsignarDia(8, Turno1Id, CopiaTurno, 1);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.Asignado);
        Emitidos(plantilla).Should().ContainSingle().Which.Dia.Should().Be(8);
    }

    [Fact]
    public void AsignarDia_RetornaDiaFueraDelCiclo_CuandoElDiaSuperaLosDiasDelCiclo()
    {
        var plantilla = CrearPlantilla(8);

        var resultado = plantilla.AsignarDia(9, Turno1Id, CopiaTurno, 1);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.DiaFueraDelCiclo);
        Emitidos(plantilla).Should().BeEmpty();
    }

    [Fact]
    public void AsignarDia_RetornaAsignado_CuandoReemplazaElTurnoDeUnDiaOcupado()
    {
        var plantilla = CrearPlantilla(8);
        plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 1);

        var resultado = plantilla.AsignarDia(3, Turno2Id, CopiaTurno, 1);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.Asignado);
        Emitidos(plantilla).Should().HaveCount(2).And.Subject.Last().TurnoId.Should().Be(Turno2Id);
    }

    [Fact]
    public void AsignarDia_RetornaSinCambios_CuandoElMismoTurnoYaEstaConCopiaVigente()
    {
        var plantilla = CrearPlantilla(8);
        plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 2);

        var resultado = plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 2);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.SinCambios);
        Emitidos(plantilla).Should().ContainSingle();
    }

    [Fact]
    public void AsignarDia_RetornaSinCambios_CuandoLaCopiaGuardadaEsMasNuevaQueLaOfrecida()
    {
        var plantilla = CrearPlantilla(8);
        plantilla.AsignarDia(3, Turno1Id, CopiaTurnoEditado, 5);

        var resultado = plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 2);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.SinCambios);
        Emitidos(plantilla).Should().ContainSingle();
    }

    [Fact]
    public void AsignarDia_RetornaAsignadoConLaCopiaVigente_CuandoLaCopiaGuardadaEstaAtrasada()
    {
        var plantilla = CrearPlantilla(8);
        plantilla.AsignarDia(3, Turno1Id, CopiaTurno, 1);

        var resultado = plantilla.AsignarDia(3, Turno1Id, CopiaTurnoEditado, 2);

        resultado.Should().Be(ResultadoAsignarDiaDePlantillaTurnos.Asignado);
        var ultimo = Emitidos(plantilla).Should().HaveCount(2).And.Subject.Last();
        ultimo.Turno.Should().Be(CopiaTurnoEditado);
        ultimo.VersionTurno.Should().Be(2);
    }
}
