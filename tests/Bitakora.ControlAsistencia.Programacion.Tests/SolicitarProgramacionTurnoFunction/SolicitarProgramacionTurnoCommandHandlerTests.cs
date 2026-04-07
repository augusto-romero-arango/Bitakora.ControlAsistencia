// HU-10: Solicitar programacion de turno del catalogo

using AwesomeAssertions;
using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction.Eventos;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.SolicitarProgramacionTurnoFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;
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

    private static readonly SolicitarProgramacionTurno.DatosEmpleado DatosEmpleado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    private static readonly InformacionEmpleado EmpleadoEsperado =
        new("E001", "CC", "12345678", "Juan", "Perez");

    // El DetalleTurno esperado corresponde al catalogo creado en CrearCatalogoConUnaFranja()
    private static readonly DetalleTurno DetalleEsperado = new(
        "Turno Manana",
        new List<DetalleFranjaOrdinaria>
        {
            new(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [])
        }.AsReadOnly());

    // --- Configuracion del handler ---

    // El handler del camino feliz usa un EventStore que tambien conoce el CatalogoTurnos
    protected override ICommandHandlerAsync<SolicitarProgramacionTurno> Handler =>
        new SolicitarProgramacionTurnoCommandHandler(
            new EventStoreConCatalogo(EventStore, CrearCatalogoConUnaFranja(), TurnoId),
            PublicEventSender);

    // --- Factory methods ---

    private static CatalogoTurnos CrearCatalogoConUnaFranja()
    {
        var evento = TurnoCreado.Crear(new CrearTurno(
            TurnoId,
            "Turno Manana",
            [new CrearTurno.Franja(new TimeOnly(6, 0), new TimeOnly(14, 0), [], [])]));
        return CatalogoTurnos.Iniciar(evento);
    }

    private static ProgramacionTurnoSolicitada EventoSolicitudParaFecha1() =>
        new(Guid.Empty, EmpleadoEsperado, [Fecha1], DetalleEsperado);

    // --- Tests del camino feliz ---

    // CA-9, CA-10, CA-11, CA-12: emite evento de ES y publica evento publico por cada fecha
    [Fact]
    public async Task DebeEmitirProgramacionSolicitadaYPublicarEvento_CuandoDatosValidos()
    {
        Given();
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, DatosEmpleado, [Fecha1]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoEsperado, [Fecha1], DetalleEsperado));
        ThenIsPublishedPublicly(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoEsperado, Fecha1, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 1);
    }

    // CA-11, CA-12: publica un evento publico por cada fecha (N fechas = N eventos)
    [Fact]
    public async Task DebePublicarUnEventoPorCadaFecha_CuandoHayMultiplesFechas()
    {
        Given();
        await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, DatosEmpleado, [Fecha1, Fecha2]));

        Then(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoEsperado, [Fecha1, Fecha2], DetalleEsperado));
        ThenIsPublishedPublicly(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoEsperado, Fecha1, DetalleEsperado));
        ThenIsPublishedPublicly(new ProgramacionTurnoDiarioSolicitada(
            GuidAggregateId, EmpleadoEsperado, Fecha2, DetalleEsperado));
        And<SolicitudProgramacionAggregateRoot, int>(s => s.Fechas.Count, 2);
    }

    // CA-6: idempotencia - solicitud ya existe lanza excepcion que el endpoint mapea a 409
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoSolicitudYaExiste()
    {
        Given(new ProgramacionTurnoSolicitada(
            GuidAggregateId, EmpleadoEsperado, [Fecha1], DetalleEsperado));

        var act = async () => await WhenAsync(new SolicitarProgramacionTurno(
            GuidAggregateId, TurnoId, DatosEmpleado, [Fecha1]));

        await act.Should().ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.SolicitudYaExiste}*");
    }

    // CA-7: turno no existe en el catalogo - lanza excepcion que el endpoint mapea a 404
    [Fact]
    public async Task DebeLanzarExcepcion_CuandoTurnoNoExisteEnElCatalogo()
    {
        var handlerSinCatalogo = new SolicitarProgramacionTurnoCommandHandler(
            new EventStoreConCatalogo(EventStore, catalogo: null, TurnoId),
            PublicEventSender);

        var act = async () => await handlerSinCatalogo.HandleAsync(
            new SolicitarProgramacionTurno(GuidAggregateId, TurnoId, DatosEmpleado, [Fecha1]));

        await act.Should().ThrowExactlyAsync<KeyNotFoundException>()
            .WithMessage($"*{SolicitarProgramacionTurnoCommandHandler.Mensajes.TurnoNoEncontrado}*");
    }

    // --- Fake manual: EventStore que ademas conoce el CatalogoTurnos ---

    /// <summary>
    /// Wrapper de IEventStore que intercepta GetAggregateRootAsync para CatalogoTurnos
    /// y sirve el catalogo pre-configurado. Todas las demas operaciones delegan al inner.
    /// </summary>
    private sealed class EventStoreConCatalogo : IEventStore
    {
        private readonly IEventStore _inner;
        private readonly CatalogoTurnos? _catalogo;
        private readonly Guid _turnoId;

        public EventStoreConCatalogo(IEventStore inner, CatalogoTurnos? catalogo, Guid turnoId)
        {
            _inner = inner;
            _catalogo = catalogo;
            _turnoId = turnoId;
        }

        public Task<bool> ExistsAsync<T>(Guid id, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.ExistsAsync<T>(id, ct);

        public Task<bool> ExistsAsync<T>(string id, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.ExistsAsync<T>(id, ct);

        public Task<T?> GetAggregateRootAsync<T>(Guid id, CancellationToken ct = default)
            where T : AggregateRoot
        {
            if (typeof(T) == typeof(CatalogoTurnos) && id == _turnoId)
                return Task.FromResult(_catalogo as T);
            return _inner.GetAggregateRootAsync<T>(id, ct);
        }

        public Task<T?> GetAggregateRootAsync<T>(string id, CancellationToken ct = default)
            where T : AggregateRoot
        {
            if (typeof(T) == typeof(CatalogoTurnos) && id == _turnoId.ToString())
                return Task.FromResult(_catalogo as T);
            return _inner.GetAggregateRootAsync<T>(id, ct);
        }

        public Task<T?> GetAggregateRootAsync<T>(Guid id, int version, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.GetAggregateRootAsync<T>(id, version, ct);

        public Task<T?> GetAggregateRootAsync<T>(string id, int version, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.GetAggregateRootAsync<T>(id, version, ct);

        public Task<T?> GetAggregateRootAsync<T>(Guid id, DateTimeOffset? timestamp, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.GetAggregateRootAsync<T>(id, timestamp, ct);

        public Task<T?> GetAggregateRootAsync<T>(string id, DateTimeOffset? timestamp, CancellationToken ct = default)
            where T : AggregateRoot
            => _inner.GetAggregateRootAsync<T>(id, timestamp, ct);

        public void StartStream(AggregateRoot aggregate)
            => _inner.StartStream(aggregate);

        public void AppendEvent(Guid streamId, object @event)
            => _inner.AppendEvent(streamId, @event);

        public void AppendEvent(string streamId, object @event)
            => _inner.AppendEvent(streamId, @event);

        public Task SaveChangesAsync(CancellationToken ct = default)
            => _inner.SaveChangesAsync(ct);
    }
}
