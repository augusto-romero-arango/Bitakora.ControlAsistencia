using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using EventoBus = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado;

public class DiaDepuradoEventHandlerPertenenciaTests : PrivateEventHandlerAsyncTest<EventoBus.DiaDepurado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"dc:{CodigoColaborador}:20260315";

    protected override IPrivateEventHandlerAsync<EventoBus.DiaDepurado> Handler =>
        new DiaDepuradoEventHandler(EventStore);

    [Fact]
    public async Task DiaDepurado_ConservaLaPertenenciaDeCadaMarcacion_CuandoElBusLaTrae()
    {
        var recibido = new EventoBus.DiaDepurado(
            CodigoColaborador, Fecha, null, "Turno Manana", [],
            [
                new EventoBus.MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new EventoBus.MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), []));

        await WhenAsync(recibido);

        Then(StreamId, new DepuracionDiaRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Turno Manana", [],
            [
                new MarcacionDelDia(new DateTime(2026, 3, 15, 4, 30, 0), "ENTRADA", Pertenencia: "Ambigua"),
                new MarcacionDelDia(new DateTime(2026, 3, 15, 6, 0, 0), "ENTRADA", Pertenencia: "DelTurno")
            ],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Provisional);
    }
}
