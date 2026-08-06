// HU-10: Solicitar programacion de turno del catalogo

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.PublicEvents.Empleados;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SolicitarProgramacionTurnoFunction;

public class SolicitarProgramacionTurnoCommandHandlerTests
    : CommandHandlerAsyncTest<SolicitarProgramacionTurno>
{
    // --- Constantes de prueba ---
    private static readonly Guid TurnoId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-aabbccddeeff");
    private static readonly Guid TurnoConHijasId =
        Guid.Parse("018e4c1a-4f2b-7000-8000-112233445566");
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    private static readonly InformacionEmpleado Empleado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // Mismo empleado, en la forma que el handler debe producir para el evento privado
    // (CA-ADR-0029 decision #5): si el mapeo pierde o permuta un campo, estos tests lo delatan.
    private static readonly DetalleEmpleado EmpleadoDetalle =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // Issue #319 CA-2/CA-5: mismo empleado, en el record propio de Programacion.DomainEvents que
    // ahora tipa ProgramacionTurnoSolicitada.Empleado (tres islas, MEF-ADR-0039 decision 2).
    private static readonly Empleado EmpleadoProgramado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // El DetalleTurno esperado corresponde al catalogo creado en CrearEventoTurno(). Forma de BUS
    // (PrivateEvents) -- solo se usa en ThenIsPublishedPrivately (CA-5: unico punto de mapeo).
    // Issue #288 CA-2: Descripcion lleva el texto real que produce el ToString() del tipo rico
    // (CatalogoTurnos a nivel turno, FranjaOrdinaria a nivel franja). La coherencia entre ambos
    // la prueban CatalogoTurnosTests y FranjaOrdinariaToDetalleTests; aqui el valor literal
    // documenta que fluye intacto hasta el evento emitido y el publicado por el bus privado.
    private static readonly DetalleTurno DetalleEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    // Issue #319 CA-1/CA-5: mismo turno, en el record propio del dominio (Programacion.DomainEvents)
    // que ahora tipa el evento persistido ProgramacionTurnoSolicitada.DetalleTurno.
    private static readonly TurnoProgramado TurnoProgramadoEsperado = new(
        "Turno Manana",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "(06:00-14:00)")
        }.AsReadOnly(),
        "Turno Manana (06:00-14:00)");

    // --- Configuracion del handler ---

    protected override ICommandHandlerAsync<SolicitarProgramacionTurno> Handler =>
        new SolicitarProgramacionTurnoCommandHandler(EventStore, PrivateEventSender);

    // --- Factory methods ---

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(
            TurnoId,
            "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    // Turno CON descansos y extras: es el unico camino que ejercita el nivel mas interno del mapeo
    // que el issue #319 introdujo en el handler (SubFranjaProgramada -> DetalleSubFranja) y la
    // recursion de las dos listas en MapearFranja. Con el turno sin hijas de arriba esos dos mapeos
    // no se ejecutan nunca, asi que perder la Descripcion de una sub-franja o permutar las listas
    // Descansos/Extras pasaba en verde -- verificado por mutacion en la revision de este PR.
    private static TurnoCreado CrearEventoTurnoConHijas() =>
        TurnoCreado.Crear(
            TurnoConHijasId,
            "Turno Partido",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0),
                [(new TimeOnly(10, 0), new TimeOnly(10, 15))],
                [(new TimeOnly(13, 0), new TimeOnly(14, 0))])]);

    // --- Formas esperadas del turno con hijas, en los dos roles de payload (CA-1, CA-5) ---
    //
    // Los literales de Descripcion son los que producen los ToString() de los tipos ricos
    // (SubFranja, FranjaOrdinaria, CatalogoTurnos); su coherencia la prueban FranjaOrdinariaToDetalleTests
    // y CatalogoTurnosTests, aqui documentan que fluyen intactos por AMBOS mapeos.

    private const string DescripcionDescanso = "(10:00-10:15)";
    private const string DescripcionExtra = "(13:00-14:00)";
    private const string DescripcionFranjaConHijas =
        "(06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-14:00)]";
    private const string DescripcionTurnoConHijas =
        "Turno Partido (06:00-14:00)[Descansos:(10:00-10:15)][Extras:(13:00-14:00)]";

    private static readonly TurnoProgramado TurnoConHijasProgramadoEsperado = new(
        "Turno Partido",
        new List<FranjaProgramada>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new SubFranjaProgramada(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, DescripcionDescanso)],
                [new SubFranjaProgramada(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, DescripcionExtra)],
                DescripcionFranjaConHijas)
        }.AsReadOnly(),
        DescripcionTurnoConHijas);

    private static readonly DetalleTurno DetalleConHijasEsperado = new(
        "Turno Partido",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new DetalleSubFranja(new TimeOnly(10, 0), new TimeOnly(10, 15), 0, 0, DescripcionDescanso)],
                [new DetalleSubFranja(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, DescripcionExtra)],
                DescripcionFranjaConHijas)
        }.AsReadOnly(),
        DescripcionTurnoConHijas);

    // --- Tests del camino feliz ---

    // CA-9, CA-10, CA-11, CA-12: emite evento de ES y publica evento publico por cada fecha
    [Fact]
    public async Task DebeEmitirProgramacionSolicitadaYPublicarEvento_CuandoDatosValidos()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoProgramado, [Fecha1], TurnoProgramadoEsperado));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoDetalle, Fecha1, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // Issue #319 CA-1/CA-5: el turno con descansos y extras recorre la jerarquia COMPLETA de los
    // dos payloads -- TurnoProgramado/FranjaProgramada/SubFranjaProgramada en el evento persistido y
    // DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja en el que cruza el bus interno. Delata que
    // el mapeo del FA (unico punto de traduccion) pierda un campo anidado o permute las dos listas
    // de sub-franjas, el modo de fallo silencioso que CA-ADR-0029 decision #5 documenta.
    // Limite conocido: DatosFranja no permite declarar offsets de sub-franja, asi que por este camino
    // DiaOffsetInicio y DiaOffsetFin de las hijas siempre valen 0 y una permutacion entre ambos no
    // seria observable; los demas campos si lo son.
    [Fact]
    public async Task SolicitarProgramacionTurno_MapeaLaJerarquiaCompletaEnLosDosPayloads_CuandoElTurnoTieneDescansosYExtras()
    {
        Given(TurnoConHijasId.ToString(), CrearEventoTurnoConHijas());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoConHijasId, Empleado, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoProgramado, [Fecha1], TurnoConHijasProgramadoEsperado));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoDetalle, Fecha1, DetalleConHijasEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Descansos.Count, 1);
        And<SolicitudProgramacionAggregateRoot, int>(
            s => s.DetalleTurno!.FranjasOrdinarias[0].Extras.Count, 1);
    }

    // CA-11, CA-12: publica un evento publico por cada fecha (N fechas = N eventos)
    [Fact]
    public async Task DebePublicarUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1, Fecha2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoProgramado, [Fecha1, Fecha2], TurnoProgramadoEsperado));
        ThenIsPublishedPrivately(
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, EmpleadoDetalle, Fecha1, DetalleEsperado),
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, EmpleadoDetalle, Fecha2, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 2);
    }

    // CA-6: idempotencia - solicitud ya existe lanza excepcion que el endpoint mapea a 409
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoSolicitudYaExiste()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        Given(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoProgramado, [Fecha1], TurnoProgramadoEsperado));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.SolicitudYaExiste}*");
    }

    // CA-7: turno no existe en el catalogo - lanza excepcion que el endpoint mapea a 404
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoTurnoNoExisteEnElCatalogo()
    {
        var act = async () => await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1]));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoNoEncontrado}*");
    }
}
