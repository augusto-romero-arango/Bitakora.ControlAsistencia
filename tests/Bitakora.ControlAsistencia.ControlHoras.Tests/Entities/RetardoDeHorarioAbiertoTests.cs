using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using RegistroDeMarcacionCreado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.RegistroDeMarcacionCreado;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class RetardoDeHorarioAbiertoTests
    : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private static readonly ColaboradorProgramado Colaborador = new(
        "CC-1234567890", "EMP-001", "Luis Augusto Barreto");

    private static readonly DateOnly Fecha = new(2026, 3, 11);
    private static readonly string StreamId = $"cd:{Colaborador.CodigoColaborador}:{Fecha:yyyyMMdd}";
    private static readonly Guid SolicitudId = Guid.Parse("019600d0-0000-7000-8000-000000000002");

    private const int VentanaDe14Horas = 840;

    private static readonly TurnoDiario AbiertoDe6Horas = new(
        "Abierto 6h",
        [new FranjaProgramada(new TimeOnly(4, 0), new TimeOnly(10, 0), 0, [], [], "")],
        "",
        VentanaDe14Horas,
        EsHorarioAbierto: true);

    private static readonly TurnoDiario FijoConVentanaDe3Horas = new(
        "Fijo con ventana",
        [new FranjaProgramada(new TimeOnly(7, 0), new TimeOnly(16, 0), 0, [], [], "")],
        "",
        180);

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static MarcacionAdicionada Marcacion(DateTime timestamp) =>
        new(StreamId, Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static TurnoDiarioAsignado Asignado(TurnoDiario turno) =>
        new(StreamId, Colaborador, Fecha, turno, SolicitudId);

    private static RegistroDeMarcacionCreado Creado(DateTime timestamp) =>
        new(Colaborador.CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    // La salida tras medianoche se guarda en el stream del dia siguiente y llega al del turno como copia
    // del reparto (CA-ADR-0042): por eso va en el Given y la entrada del propio dia dispara el handler.
    private static decimal HorasSinRetardo(ControlDiarioAggregateRoot c) =>
        c.CrearDiaDepurado().HorasDiscriminadas.HorasPorConcepto
            .Where(h => h.Key != "Retardo")
            .Sum(h => h.Value);

    // CA-1: franja efectiva 18:00-24:00, entrada 19:00 -> retardo 60 sin excedente que lo compense.
    // En un abierto no se castiga: sin clave "Retardo"; las 5 h trabajadas se liquidan igual.
    [Fact]
    public async Task CrearDiaDepurado_NoLlevaRetardo_CuandoAbiertoEntraTardeSinExcedente()
    {
        var entrada = new DateTime(2026, 3, 11, 19, 0, 0);
        var salida = new DateTime(2026, 3, 12, 0, 0, 0);
        Given(StreamId, Asignado(AbiertoDe6Horas), Marcacion(salida));

        await WhenAsync(Creado(entrada));

        Then(StreamId, Marcacion(entrada));
        And<ControlDiarioAggregateRoot, bool>(
            StreamId,
            c => c.CrearDiaDepurado().HorasDiscriminadas.HorasPorConcepto.ContainsKey("Retardo"),
            false);
        And<ControlDiarioAggregateRoot, decimal>(StreamId, HorasSinRetardo, 5.00m);
    }

    // CA-2: entrada 19:00, salida 01:00 -> retardo 60 compensado con 00:00-01:00: sin retardo y sin extra.
    [Fact]
    public async Task CrearDiaDepurado_ConservaCompensacionIntraFranja_CuandoAbiertoSaleDespuesDelCierre()
    {
        var entrada = new DateTime(2026, 3, 11, 19, 0, 0);
        var salida = new DateTime(2026, 3, 12, 1, 0, 0);
        Given(StreamId, Asignado(AbiertoDe6Horas), Marcacion(salida));

        await WhenAsync(Creado(entrada));

        Then(StreamId, Marcacion(entrada));
        And<ControlDiarioAggregateRoot, bool>(
            StreamId,
            c => c.CrearDiaDepurado().HorasDiscriminadas.HorasPorConcepto.ContainsKey("Retardo"),
            false);
        And<ControlDiarioAggregateRoot, decimal>(StreamId, HorasSinRetardo, 5.00m);
    }

    // CA-3: un turno fijo con ventana limitada conserva su retardo: franja efectiva 10:00-19:00,
    // entrada 10:30 -> retardo neto 30 (0.50 h); trabajadas 10:30-19:00 = 8.5 h.
    [Fact]
    public async Task CrearDiaDepurado_ConservaElRetardo_CuandoTurnoFijoEntraTarde()
    {
        var entrada = new DateTime(2026, 3, 11, 10, 30, 0);
        var salida = new DateTime(2026, 3, 11, 19, 0, 0);
        Given(StreamId, Asignado(FijoConVentanaDe3Horas), Marcacion(entrada));

        await WhenAsync(Creado(salida));

        Then(StreamId, Marcacion(salida));
        And<ControlDiarioAggregateRoot, decimal>(
            StreamId,
            c => c.CrearDiaDepurado().HorasDiscriminadas.HorasPorConcepto["Retardo"],
            0.50m);
        And<ControlDiarioAggregateRoot, decimal>(StreamId, HorasSinRetardo, 8.50m);
    }
}
