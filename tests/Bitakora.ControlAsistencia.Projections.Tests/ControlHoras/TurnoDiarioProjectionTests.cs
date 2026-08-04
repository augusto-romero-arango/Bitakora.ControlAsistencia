// Issue #289: fase roja de la primera proyeccion concreta del BC. Invocacion DIRECTA de los
// metodos estaticos de TurnoDiarioProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply bajo prueba para construir el valor esperado.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class TurnoDiarioProjectionTests
{
    private static InformacionEmpleado EmpleadoDePrueba() =>
        new("EMP-001", "CC", "1098765432", "Ana", "Ramirez");

    private static DetalleTurno DetalleTurnoDePrueba(string nombre) =>
        new(
            nombre,
            [
                new DetalleFranjaOrdinaria(
                    new TimeOnly(6, 0),
                    new TimeOnly(14, 0),
                    DiaOffsetFin: 0,
                    Descansos: [new DetalleSubFranja(
                        new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)")],
                    Extras: [],
                    Descripcion: "(06:00-14:00)[Descansos:(10:00-10:15)]")
            ],
            $"{nombre}: (06:00-14:00)[Descansos:(10:00-10:15)]");

    // CA-2/CA-5: Create mapea los cinco campos de la vista desde el evento fundacional, incluida
    // la estructura completa de franjas anidadas (descansos/extras) que trae DetalleTurno.
    [Fact]
    public void Create_ProyectaElTurnoDiarioVigente_DesdeTurnoDiarioAsignado()
    {
        var empleado = EmpleadoDePrueba();
        var detalleTurno = DetalleTurnoDePrueba("Turno Manana");
        var fecha = new DateOnly(2026, 8, 3);
        var solicitudId = Guid.NewGuid();
        var evento = new TurnoDiarioAsignado("EMP-001:2026-08-03", empleado, fecha, detalleTurno, solicitudId);

        var vista = TurnoDiarioProjection.Create(evento);

        vista.Should().Be(new TurnoDiarioView(
            "EMP-001:2026-08-03", empleado, fecha, detalleTurno, solicitudId));
    }

    // CA-3: la reasignacion sobrescribe -- dos TurnoDiarioAsignado consecutivos sobre el mismo
    // (empleado, fecha) dejan la vista con el DetalleTurno y la UltimaSolicitudId del SEGUNDO. El
    // Id, el Empleado y la Fecha de la identidad del documento no cambian (mismo stream key).
    [Fact]
    public void Apply_SobrescribeDetalleTurnoYUltimaSolicitud_CuandoLlegaOtroTurnoDiarioAsignado()
    {
        var empleado = EmpleadoDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var vistaPrevia = new TurnoDiarioView(
            "EMP-001:2026-08-03", empleado, fecha, DetalleTurnoDePrueba("Turno Manana"), Guid.NewGuid());
        var nuevoDetalleTurno = DetalleTurnoDePrueba("Turno Tarde");
        var nuevaSolicitudId = Guid.NewGuid();
        var segundoEvento = new TurnoDiarioAsignado(
            vistaPrevia.Id, empleado, fecha, nuevoDetalleTurno, nuevaSolicitudId);

        var vista = TurnoDiarioProjection.Apply(segundoEvento, vistaPrevia);

        vista.Should().Be(new TurnoDiarioView(
            vistaPrevia.Id, empleado, fecha, nuevoDetalleTurno, nuevaSolicitudId));
    }
}
