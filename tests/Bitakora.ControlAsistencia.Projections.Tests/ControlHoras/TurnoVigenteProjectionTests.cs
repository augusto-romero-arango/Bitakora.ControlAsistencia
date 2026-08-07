// Issue #328: fase roja de la segunda proyeccion concreta del BC. Invocacion DIRECTA de los
// metodos estaticos de TurnoVigenteProjection (N1, MEF-ADR-0035) -- no el DSL Given/When/Then de
// CommandHandlerTestBase (MEF-ADR-0002, testea command handlers contra el event store): aqui se
// testean funciones puras evento -> vista, sin abrir ningun stream.
//
// Cada assert compara contra un oraculo armado a mano (MEF-ADR-0002, no-tautologia): nunca se
// reusa la logica de Create/Apply bajo prueba para construir el valor esperado. Los Bloques
// esperados SI se calculan aplicando a mano el algoritmo documentado de TurnoDiario.Segmentar
// (issue #327, ya cubierto por sus propios tests) -- Segmentar no es la logica bajo prueba en este
// archivo; lo que se verifica aqui es que Create/Apply lo invocan sobre el payload del evento
// (Tell-don't-Ask, MEF-ADR-0012) y mapean cada BloqueTurno resultante al record Bloque propio de
// la vista (ReadModels, sin relacion de tipo con DomainEvents).
//
// using TipoBloqueVigente: alias obligatorio -- DomainEvents.TipoBloque (necesario para construir
// el evento fundacional) y ReadModels.ControlHoras.TipoBloque (necesario para el oraculo de la
// vista) comparten nombre a proposito, mismo criterio de "tres islas" que ya aplican Empleado/
// TurnoDiario/FranjaProgramada entre los ensamblados de eventos. Con ambos "using" activos el
// simbolo corto "TipoBloque" queda ambiguo (CS0104); el alias resuelve solo el lado ReadModels.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.Projections.ControlHoras;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using TipoBloqueVigente = Bitakora.ControlAsistencia.ReadModels.ControlHoras.TipoBloque;

namespace Bitakora.ControlAsistencia.Projections.Tests.ControlHoras;

public class TurnoVigenteProjectionTests
{
    private static Empleado EmpleadoDePrueba() =>
        new("EMP-001", "CC", "1098765432", "Ana", "Ramirez");

    // CA-1: Create mapea stream key, EmpleadoId, NombreCompleto (concatenado Nombres+Apellidos --
    // unico lugar del sistema donde se hace, issue #328 "Investigacion del planner"), NombreTurno,
    // HorarioResumido (la Descripcion textual que el evento ya trae) y los Bloques que produce
    // Segmentar, con los tres tipos posibles (Ordinaria/Descanso/Extra) representados.
    [Fact]
    public void Create_ProyectaElTurnoVigenteCompleto_DesdeTurnoDiarioAsignado()
    {
        var empleado = EmpleadoDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "EMP-001:2026-08-03";

        // Franja 06:00-14:00 con un descanso (10:00-10:15) y un extra ANTES del inicio nominal
        // (05:00-06:00): cubre los tres TipoBloque sin que ningun tramo cruce medianoche, para
        // poder calcular el oraculo a mano sin ambiguedad (Tramo.RomperEnMedianoche no interviene).
        var franja = new FranjaProgramada(
            new TimeOnly(6, 0),
            new TimeOnly(14, 0),
            DiaOffsetFin: 0,
            Descansos: [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, "(10:00-10:15)")],
            Extras: [new SubFranjaProgramada(new TimeOnly(5, 0), new TimeOnly(6, 0), 0, 0, "(05:00-06:00)")],
            Descripcion: "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        var turnoDiario = new TurnoDiario(
            "Turno Manana",
            [franja],
            "Turno Manana: (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        var solicitudId = Guid.NewGuid();
        var evento = new TurnoDiarioAsignado(streamKey, empleado, fecha, turnoDiario, solicitudId);

        var vista = TurnoVigenteProjection.Create(evento);

        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);
        var bloquesEsperados = new[]
        {
            new Bloque(TipoBloqueVigente.Extra, medianoche.AddHours(5), medianoche.AddHours(6)),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(10)),
            new Bloque(TipoBloqueVigente.Descanso, medianoche.AddHours(10), medianoche.AddHours(10).AddMinutes(15)),
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(10).AddMinutes(15), medianoche.AddHours(14)),
        };

        vista.Id.Should().Be(streamKey);
        vista.EmpleadoId.Should().Be("EMP-001");
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.Fecha.Should().Be(fecha);
        vista.NombreTurno.Should().Be("Turno Manana");
        vista.HorarioResumido.Should().Be(
            "Turno Manana: (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(05:00-06:00)]");
        vista.Bloques.Should().Equal(bloquesEsperados);
    }

    // CA-2: la reasignacion sobrescribe -- dos TurnoDiarioAsignado consecutivos sobre el mismo
    // (empleado, fecha) dejan la vista con el NombreTurno, HorarioResumido y Bloques del SEGUNDO
    // evento ("el ultimo gana"). Id, EmpleadoId y Fecha no cambian (mismo stream key). Sin
    // ShouldDelete (el turno vigente nunca se borra, solo se reasigna) -- no hay metodo que probar.
    [Fact]
    public void Apply_SobrescribeTurnoHorarioYBloques_CuandoLlegaOtroTurnoDiarioAsignado()
    {
        var empleado = EmpleadoDePrueba();
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "EMP-001:2026-08-03";
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

        var vistaPrevia = new TurnoVigente(
            streamKey,
            "EMP-001",
            "Ana Ramirez",
            fecha,
            "Turno Manana",
            "Turno Manana: (06:00-14:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14))]);

        // Segundo turno, sin descansos ni extras: un solo bloque Ordinaria 14:00-22:00.
        var franjaTarde = new FranjaProgramada(
            new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
            Descansos: [], Extras: [], Descripcion: "(14:00-22:00)");
        var turnoTarde = new TurnoDiario("Turno Tarde", [franjaTarde], "Turno Tarde: (14:00-22:00)");
        var nuevaSolicitudId = Guid.NewGuid();
        var segundoEvento = new TurnoDiarioAsignado(streamKey, empleado, fecha, turnoTarde, nuevaSolicitudId);

        var vista = TurnoVigenteProjection.Apply(segundoEvento, vistaPrevia);

        vista.Id.Should().Be(streamKey);
        vista.EmpleadoId.Should().Be("EMP-001");
        vista.Fecha.Should().Be(fecha);
        vista.NombreCompleto.Should().Be("Ana Ramirez");
        vista.NombreTurno.Should().Be("Turno Tarde");
        vista.HorarioResumido.Should().Be("Turno Tarde: (14:00-22:00)");
        vista.Bloques.Should().Equal(
            new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(14), medianoche.AddHours(22)));
    }

    // CA-2 (borde que el test de arriba no discrimina, porque ahi los dos eventos traen el MISMO
    // empleado): cada TurnoDiarioAsignado carga el payload Empleado completo, asi que una correccion
    // del nombre aguas arriba llega con la reasignacion y el "ultimo gana" tambien le aplica --
    // congelar el nombre de la primera asignacion dejaria la vista mostrando un dato viejo para
    // siempre. Id, EmpleadoId y Fecha si son invariantes (identidad del stream), y se verifican aqui
    // junto al refresco para que el test no pueda pasar por sobrescribir la vista entera.
    [Fact]
    public void Apply_RefrescaElNombreCompleto_CuandoLaReasignacionTraeElNombreCorregido()
    {
        var fecha = new DateOnly(2026, 8, 3);
        var streamKey = "EMP-001:2026-08-03";
        var medianoche = fecha.ToDateTime(TimeOnly.MinValue);

        var vistaPrevia = new TurnoVigente(
            streamKey,
            "EMP-001",
            "Ana Ramirez",
            fecha,
            "Turno Manana",
            "Turno Manana: (06:00-14:00)",
            [new Bloque(TipoBloqueVigente.Ordinaria, medianoche.AddHours(6), medianoche.AddHours(14))]);

        // Mismo EmpleadoId, nombre corregido aguas arriba (dos nombres y dos apellidos).
        var empleadoCorregido = new Empleado("EMP-001", "CC", "1098765432", "Ana Maria", "Ramirez Solano");
        var turnoTarde = new TurnoDiario(
            "Turno Tarde",
            [new FranjaProgramada(
                new TimeOnly(14, 0), new TimeOnly(22, 0), DiaOffsetFin: 0,
                Descansos: [], Extras: [], Descripcion: "(14:00-22:00)")],
            "Turno Tarde: (14:00-22:00)");
        var segundoEvento = new TurnoDiarioAsignado(
            streamKey, empleadoCorregido, fecha, turnoTarde, Guid.NewGuid());

        var vista = TurnoVigenteProjection.Apply(segundoEvento, vistaPrevia);

        vista.NombreCompleto.Should().Be("Ana Maria Ramirez Solano");
        vista.Id.Should().Be(streamKey);
        vista.EmpleadoId.Should().Be("EMP-001");
        vista.Fecha.Should().Be(fecha);
    }
}
