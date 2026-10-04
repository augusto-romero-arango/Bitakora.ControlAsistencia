using AwesomeAssertions;
using Bitakora.ControlAsistencia.Mcp.Asistente.AplicarPlantillaSemanal;
using Bitakora.ControlAsistencia.Mcp.Asistente.Infraestructura;

namespace Bitakora.ControlAsistencia.Mcp.Asistente.Tests.AplicarPlantillaSemanal;

public class AsignacionDePlantillaTests
{
    private const string TurnoSemana1 = "00000000-0000-7000-9000-000000000001";
    private const string TurnoSemana2 = "00000000-0000-7000-9000-000000000002";

    private static TurnoDelCuadro Turno(string id) => new(id, $"Turno {id[^1]}", "(06:00-14:00)", true, false);

    private static CuadroSemanalTurnos MoldeDeDosSemanas() => new(
        "plantilla-1", "Dos semanas", 2, true,
        [.. Enumerable.Range(1, 7).Select(d => new DiaDelCuadro(1, d, Turno(TurnoSemana1))),
         .. Enumerable.Range(1, 7).Select(d => new DiaDelCuadro(2, d, Turno(TurnoSemana2)))]);

    [Fact]
    public void Para_VuelveALaSemanaUno_CuandoLaVentanaDeTresSemanasSuperaLasSemanasDelMolde()
    {
        var asignacion = AsignacionDePlantilla.Crear(MoldeDeDosSemanas(), new DateOnly(2026, 8, 31));

        asignacion.Para(new DateOnly(2026, 8, 31)).Id.Should().Be(TurnoSemana1);
        asignacion.Para(new DateOnly(2026, 9, 7)).Id.Should().Be(TurnoSemana2);
        asignacion.Para(new DateOnly(2026, 9, 13)).Id.Should().Be(TurnoSemana2);
        asignacion.Para(new DateOnly(2026, 9, 14)).Id.Should().Be(TurnoSemana1);
        asignacion.Para(new DateOnly(2026, 9, 20)).Id.Should().Be(TurnoSemana1);
    }

    [Fact]
    public void Para_UsaLaSemanaUnoParaLaSemanaCalendarioQueContieneDesde_CuandoLaVentanaEmpiezaAMitadDeSemana()
    {
        var asignacion = AsignacionDePlantilla.Crear(MoldeDeDosSemanas(), new DateOnly(2026, 9, 2));

        asignacion.Para(new DateOnly(2026, 9, 2)).Id.Should().Be(TurnoSemana1);
        asignacion.Para(new DateOnly(2026, 9, 6)).Id.Should().Be(TurnoSemana1);
        asignacion.Para(new DateOnly(2026, 9, 7)).Id.Should().Be(TurnoSemana2);
    }

    [Fact]
    public void Para_TomaElDiaEnISO_CuandoCadaDiaDeLaSemanaTieneUnTurnoDistinto()
    {
        var porDia = Enumerable.Range(1, 7)
            .Select(d => new DiaDelCuadro(1, d, Turno($"00000000-0000-7000-9000-00000000010{d}")))
            .ToList();
        var cuadro = new CuadroSemanalTurnos("p", "Una semana", 1, true, porDia);

        var asignacion = AsignacionDePlantilla.Crear(cuadro, new DateOnly(2026, 8, 31));

        asignacion.Para(new DateOnly(2026, 8, 31)).Id.Should().Be("00000000-0000-7000-9000-000000000101");
        asignacion.Para(new DateOnly(2026, 9, 6)).Id.Should().Be("00000000-0000-7000-9000-000000000107");
    }

    [Fact]
    public void Para_NoDependeDeLaFechaDeInicioDentroDeLaSemana_CuandoSeRecortaLaVentanaPorVigencia()
    {
        var completa = AsignacionDePlantilla.Crear(MoldeDeDosSemanas(), new DateOnly(2026, 8, 31));
        var recortada = AsignacionDePlantilla.Crear(MoldeDeDosSemanas(), new DateOnly(2026, 9, 3));

        recortada.Para(new DateOnly(2026, 9, 9)).Id.Should().Be(completa.Para(new DateOnly(2026, 9, 9)).Id);
        recortada.Para(new DateOnly(2026, 9, 9)).Id.Should().Be(TurnoSemana2);
    }
}
