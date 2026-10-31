using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionesCuandoDiasDepurados.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using EventoBus = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionesCuandoDiasDepurados;

public class DiasDepuradosVersionEventHandlerTests : PrivateEventHandlerAsyncTest<EventoBus.DiasDepurados>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha1 = new(2026, 3, 15);
    private static readonly DateOnly Fecha2 = new(2026, 3, 16);
    private static readonly string StreamId1 = $"dc:{CodigoColaborador}:20260315";
    private static readonly string StreamId2 = $"dc:{CodigoColaborador}:20260316";

    protected override IPrivateEventHandlerAsync<EventoBus.DiasDepurados> Handler =>
        new DiasDepuradosEventHandler(EventStore);

    private static EventoBus.DiaDepurado Foto(DateOnly fecha, string nombreTurno, long? version) => new(
        CodigoColaborador, fecha, null, nombreTurno, [], [],
        new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), []),
        VersionControlDiario: version);

    private static DepuracionDiaRecibida Persistida(string streamId, DateOnly fecha, string nombreTurno, long? version) => new(
        streamId, CodigoColaborador, fecha, null, nombreTurno, [], [],
        new HorasDiscriminadas(new Dictionary<string, decimal>(), []),
        versionControlDiario: version);

    private static string? TurnoDelDia(DiaCalculadoAggregateRoot dia) => dia.GenerarDepuracionDelDia().NombreTurno;

    [Fact]
    public async Task DiasDepurados_PasaLaVersionYAplicaSoloLasFotosMasNuevas_CuandoElLoteMezclaVersiones()
    {
        Given(StreamId1, Persistida(StreamId1, Fecha1, "Turno V5", 5));
        Given(StreamId2, Persistida(StreamId2, Fecha2, "Turno V5", 5));

        await WhenAsync(new EventoBus.DiasDepurados(
            CodigoColaborador, [Foto(Fecha1, "Turno V3", 3), Foto(Fecha2, "Turno V6", 6)]));

        Then(StreamId1);
        Then(StreamId2, Persistida(StreamId2, Fecha2, "Turno V6", 6));
        And<DiaCalculadoAggregateRoot, string?>(StreamId1, TurnoDelDia, "Turno V5");
        And<DiaCalculadoAggregateRoot, string?>(StreamId2, TurnoDelDia, "Turno V6");
    }

    [Fact]
    public async Task DiasDepurados_CreaElDiaConLaVersion_CuandoElDiaEsNuevo()
    {
        await WhenAsync(new EventoBus.DiasDepurados(CodigoColaborador, [Foto(Fecha1, "Turno V4", 4)]));

        Then(StreamId1, Persistida(StreamId1, Fecha1, "Turno V4", 4));
        And<DiaCalculadoAggregateRoot, string?>(StreamId1, TurnoDelDia, "Turno V4");
    }

    [Fact]
    public async Task DiasDepurados_NoEmiteEventos_CuandoElLoteRepublicaLaMismaVersion()
    {
        Given(StreamId1, Persistida(StreamId1, Fecha1, "Turno V5", 5));

        await WhenAsync(new EventoBus.DiasDepurados(CodigoColaborador, [Foto(Fecha1, "Turno repetido", 5)]));

        Then(StreamId1);
        And<DiaCalculadoAggregateRoot, string?>(StreamId1, TurnoDelDia, "Turno V5");
    }
}
