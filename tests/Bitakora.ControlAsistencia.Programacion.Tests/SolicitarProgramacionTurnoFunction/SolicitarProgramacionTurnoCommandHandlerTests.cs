// HU-10: Solicitar programacion de turno del catalogo

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.Eventos;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
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

    // El DetalleTurno esperado corresponde al catalogo creado en CrearEventoTurno()
    private static readonly DetalleTurno DetalleEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [])
        }.AsReadOnly());

    // --- Configuracion del handler ---

    protected override ICommandHandlerAsync<SolicitarProgramacionTurno> Handler =>
        new SolicitarProgramacionTurnoCommandHandler(EventStore, PublicEventSender);

    // --- Factory methods ---

    private static TurnoCreado CrearEventoTurno() =>
        TurnoCreado.Crear(new CrearTurno(
            TurnoId,
            "Turno Manana",
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]));

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
        ThenIsPublishedPublicly(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, Empleado, Fecha1, DetalleEsperado));
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
        ThenIsPublishedPublicly(
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, Empleado, Fecha1, DetalleEsperado),
            new ProgramacionTurnoDiarioSolicitada(
                GuidAggregateId, Empleado, Fecha2, DetalleEsperado));
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
