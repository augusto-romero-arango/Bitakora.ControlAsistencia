// Issue #289: fase roja de la primera proyeccion concreta del BC. Invocacion DIRECTA de los
// metodos estaticos de TurnoDiarioProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply bajo prueba para construir el valor esperado.
//
// Issue #322: TurnoDiarioAsignado ahora persiste Empleado/TurnoDiario (ControlHoras.DomainEvents,
// payload por rol) en vez de InformacionEmpleado/DetalleTurno (PublicEvents/PrivateEvents).
// TurnoDiarioView NO cambia en este issue (estado intermedio deliberado): sigue usando los tipos
// de bus, asi que estos tests construyen el evento con los tipos nuevos y esperan la vista con los
// tipos de bus -- Create/Apply son responsables del mapeo mecanico entre ambos.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class TurnoDiarioProjectionTests
{
    // Empleado (ControlHoras.DomainEvents) -- lo que trae el evento persistido.
    private static Empleado EmpleadoDePrueba() =>
        new("EMP-001", "CC", "1098765432", "Ana", "Ramirez");

    // InformacionEmpleado (PublicEvents) -- lo que espera TurnoDiarioView, sin cambios en este issue.
    private static InformacionEmpleado EmpleadoEsperadoEnVista() =>
        new("EMP-001", "CC", "1098765432", "Ana", "Ramirez");

    private static TurnoDiario TurnoDiarioDePrueba(string nombre) =>
        new(
            nombre,
            [
                new FranjaProgramada(
                    new TimeOnly(6, 0),
                    new TimeOnly(14, 0),
                    DiaOffsetFin: 0,
                    Descansos: [new SubFranjaProgramada(
                        new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)")],
                    Extras: [],
                    Descripcion: "(06:00-14:00)[Descansos:(10:00-10:15)]")
            ],
            $"{nombre}: (06:00-14:00)[Descansos:(10:00-10:15)]");

    // DetalleTurno (PrivateEvents) -- lo que espera TurnoDiarioView, con la misma forma de datos
    // que TurnoDiarioDePrueba pero en el tipo de bus (mapeo mecanico que Create/Apply deben hacer).
    private static DetalleTurno DetalleTurnoEsperadoEnVista(string nombre) =>
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
    // la estructura completa de franjas anidadas (descansos/extras) que trae TurnoDiario.
    [Fact]
    public void Create_ProyectaElTurnoDiarioVigente_DesdeTurnoDiarioAsignado()
    {
        var empleado = EmpleadoDePrueba();
        var turnoDiario = TurnoDiarioDePrueba("Turno Manana");
        var fecha = new DateOnly(2026, 8, 3);
        var solicitudId = Guid.NewGuid();
        var evento = new TurnoDiarioAsignado("EMP-001:2026-08-03", empleado, fecha, turnoDiario, solicitudId);

        var vista = TurnoDiarioProjection.Create(evento);

        vista.Should().Be(new TurnoDiarioView(
            "EMP-001:2026-08-03",
            EmpleadoEsperadoEnVista(),
            fecha,
            DetalleTurnoEsperadoEnVista("Turno Manana"),
            solicitudId));
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
            "EMP-001:2026-08-03", EmpleadoEsperadoEnVista(), fecha,
            DetalleTurnoEsperadoEnVista("Turno Manana"), Guid.NewGuid());
        var nuevoTurnoDiario = TurnoDiarioDePrueba("Turno Tarde");
        var nuevaSolicitudId = Guid.NewGuid();
        var segundoEvento = new TurnoDiarioAsignado(
            vistaPrevia.Id, empleado, fecha, nuevoTurnoDiario, nuevaSolicitudId);

        var vista = TurnoDiarioProjection.Apply(segundoEvento, vistaPrevia);

        vista.Should().Be(new TurnoDiarioView(
            vistaPrevia.Id, EmpleadoEsperadoEnVista(), fecha,
            DetalleTurnoEsperadoEnVista("Turno Tarde"), nuevaSolicitudId));
    }
}
