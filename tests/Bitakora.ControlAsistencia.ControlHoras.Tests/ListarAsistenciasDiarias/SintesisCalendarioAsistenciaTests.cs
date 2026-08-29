// Funcion pura documentos+rango -> filas: se invoca directamente, sin QuerySession/Marten y sin el
// DSL Given/When/Then de CommandHandlerTestBase, reservado a command handlers contra el event store
// (MEF-ADR-0002). Cada oraculo se arma a mano, campo por campo: nunca se reusa el mapeo bajo prueba
// para construir el esperado.
//
// HorasPorConcepto se verifica con BeEquivalentTo y nunca comparando la fila entera con
// Should().Be(...): IReadOnlyDictionary<string, decimal> no recibe equality estructural del
// compilador de records.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.ListarAsistenciasDiarias;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.ListarAsistenciasDiarias;

public class SintesisCalendarioAsistenciaTests
{
    private const string CodigoColaborador = "EMP-001";

    private static AsistenciaDiaria DocumentoDePrueba(
        DateOnly fecha,
        EstadoAsistencia estado = EstadoAsistencia.Provisional,
        PlanDelDia plan = PlanDelDia.ConJornada,
        string? nombreTurno = "Turno Manana",
        bool noSePresento = false,
        bool franjasIncompletas = false,
        bool vinoEnDescanso = false,
        bool trabajoSinProgramacion = false,
        bool conflictoDeSedePendiente = false,
        IReadOnlyDictionary<string, decimal>? horasPorConcepto = null) =>
        new(
            DiaCalculadoAggregateRoot.ComputarStreamId(CodigoColaborador, fecha),
            CodigoColaborador,
            fecha,
            estado,
            plan,
            nombreTurno,
            noSePresento,
            franjasIncompletas,
            vinoEnDescanso,
            trabajoSinProgramacion,
            conflictoDeSedePendiente,
            horasPorConcepto ?? new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m });

    [Fact]
    public void Completar_ProduceFilaConLosDatosDelDocumento_CuandoElDiaTieneDocumentoProvisional()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var horas = new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m };
        var documento = DocumentoDePrueba(fecha, horasPorConcepto: horas);

        var filas = SintesisCalendarioAsistencia.Completar(fecha, fecha, [documento]);

        filas.Should().HaveCount(1);
        var fila = filas[0];
        fila.Fecha.Should().Be(fecha);
        fila.Estado.Should().Be(EstadoAsistenciaPresentado.Provisional);
        fila.Plan.Should().Be(PlanDelDia.ConJornada);
        fila.NombreTurno.Should().Be("Turno Manana");
        fila.NoSePresento.Should().BeFalse();
        fila.FranjasIncompletas.Should().BeFalse();
        fila.VinoEnDescanso.Should().BeFalse();
        fila.TrabajoSinProgramacion.Should().BeFalse();
        fila.HorasPorConcepto.Should().BeEquivalentTo(horas);
    }

    [Fact]
    public void Completar_MapeaEstadoAprobado_CuandoElDocumentoEstaEnEstadoAprobado()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var documento = DocumentoDePrueba(fecha, estado: EstadoAsistencia.Aprobado);

        var filas = SintesisCalendarioAsistencia.Completar(fecha, fecha, [documento]);

        filas.Should().ContainSingle().Which.Estado.Should().Be(EstadoAsistenciaPresentado.Aprobado);
    }

    [Fact]
    public void Completar_ProduceFilaSinteticaSinDatos_CuandoElDiaNoTieneDocumento()
    {
        var fecha = new DateOnly(2026, 8, 3);

        var filas = SintesisCalendarioAsistencia.Completar(fecha, fecha, []);

        filas.Should().HaveCount(1);
        var fila = filas[0];
        fila.Fecha.Should().Be(fecha);
        fila.Estado.Should().Be(EstadoAsistenciaPresentado.SinDatos);
        fila.Plan.Should().Be(PlanDelDia.SinProgramar);
        fila.NombreTurno.Should().BeNull();
        fila.NoSePresento.Should().BeFalse();
        fila.FranjasIncompletas.Should().BeFalse();
        fila.VinoEnDescanso.Should().BeFalse();
        fila.TrabajoSinProgramacion.Should().BeFalse();
        fila.HorasPorConcepto.Should().BeEmpty();
    }

    [Fact]
    public void Completar_ProduceUnaFilaPorCadaDiaDelRango_EnOrdenAscendentePorFecha_CuandoSoloElDiaDelMedioTieneDocumento()
    {
        var dia1 = new DateOnly(2026, 8, 1);
        var dia2 = new DateOnly(2026, 8, 2);
        var dia3 = new DateOnly(2026, 8, 3);
        var documentoDia2 = DocumentoDePrueba(dia2);

        var filas = SintesisCalendarioAsistencia.Completar(dia1, dia3, [documentoDia2]);

        filas.Should().HaveCount(3);
        filas[0].Fecha.Should().Be(dia1);
        filas[0].Estado.Should().Be(EstadoAsistenciaPresentado.SinDatos);
        filas[1].Fecha.Should().Be(dia2);
        filas[1].Estado.Should().Be(EstadoAsistenciaPresentado.Provisional);
        filas[2].Fecha.Should().Be(dia3);
        filas[2].Estado.Should().Be(EstadoAsistenciaPresentado.SinDatos);
    }

    [Fact]
    public void Completar_ProduceTodasLasFilasSinteticas_CuandoNingunDocumentoExisteEnElRango()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 5);

        var filas = SintesisCalendarioAsistencia.Completar(desde, hasta, []);

        filas.Should().HaveCount(5);
        filas.Should().OnlyContain(f => f.Estado == EstadoAsistenciaPresentado.SinDatos);
    }

    [Fact]
    public void Completar_IgnoraUnDocumentoFueraDelRangoAplicado_CuandoSuFechaNoPerteneceAlRango()
    {
        var desde = new DateOnly(2026, 8, 1);
        var hasta = new DateOnly(2026, 8, 2);
        var documentoFueraDeRango = DocumentoDePrueba(new DateOnly(2026, 8, 10));

        var filas = SintesisCalendarioAsistencia.Completar(desde, hasta, [documentoFueraDeRango]);

        filas.Should().HaveCount(2);
        filas.Should().OnlyContain(f => f.Estado == EstadoAsistenciaPresentado.SinDatos);
    }

    [Fact]
    public void Completar_PropagaConflictoDeSedePendiente_CuandoElDocumentoLoTraeEnTrue()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var documento = DocumentoDePrueba(fecha, conflictoDeSedePendiente: true);

        var filas = SintesisCalendarioAsistencia.Completar(fecha, fecha, [documento]);

        filas.Should().ContainSingle().Which.ConflictoDeSedePendiente.Should().BeTrue();
    }

    [Fact]
    public void Completar_DejaConflictoDeSedePendienteEnFalse_EnLaFilaSinteticaSinDocumento()
    {
        var fecha = new DateOnly(2026, 8, 3);

        var filas = SintesisCalendarioAsistencia.Completar(fecha, fecha, []);

        filas.Should().ContainSingle().Which.ConflictoDeSedePendiente.Should().BeFalse();
    }
}
