// HU-10: Solicitar programacion de turno del catalogo

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
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
    private static readonly DateOnly Fecha1 = new(2026, 4, 7);
    private static readonly DateOnly Fecha2 = new(2026, 4, 8);

    private static readonly InformacionEmpleado Empleado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // Mismo empleado, en la forma que el handler debe producir para el evento privado
    // (CA-ADR-0029 decision #5): si el mapeo pierde o permuta un campo, estos tests lo delatan.
    private static readonly DetalleEmpleado EmpleadoDetalle =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // El DetalleTurno esperado corresponde al catalogo creado en CrearEventoTurno()
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

    // --- Configuracion del handler ---

    protected override ICommandHandlerAsync<SolicitarProgramacionTurno> Handler =>
        new SolicitarProgramacionTurnoCommandHandler(EventStore, PrivateEventSender);

    // --- Factory methods ---

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(
            TurnoId,
            "Turno Manana",
            [new DatosFranja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]);

    // --- Tests del camino feliz ---

    // CA-9, CA-10, CA-11, CA-12: emite evento de ES y publica evento publico por cada fecha
    [Fact]
    public async Task DebeEmitirProgramacionSolicitadaYPublicarEvento_CuandoDatosValidos()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, Empleado, [Fecha1], DetalleEsperado));
        ThenIsPublishedPrivately(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoDetalle, Fecha1, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // CA-11, CA-12: publica un evento publico por cada fecha (N fechas = N eventos)
    [Fact]
    public async Task DebePublicarUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given(TurnoId.ToString(), CrearEventoTurno());
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, Empleado, [Fecha1, Fecha2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, Empleado, [Fecha1, Fecha2], DetalleEsperado));
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
            GuidAggregateId, Empleado, [Fecha1], DetalleEsperado));

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
