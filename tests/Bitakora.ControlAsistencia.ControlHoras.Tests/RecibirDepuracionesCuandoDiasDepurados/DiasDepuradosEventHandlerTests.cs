using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionesCuandoDiasDepurados.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using EventoBus = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;
using ColaboradorBus = Bitakora.ControlAsistencia.PrivateEvents.Colaboradores.ResumenColaborador;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionesCuandoDiasDepurados;

public class DiasDepuradosEventHandlerTests : PrivateEventHandlerAsyncTest<EventoBus.DiasDepurados>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha1 = new(2026, 3, 15);
    private static readonly DateOnly Fecha2 = new(2026, 3, 16);
    private static readonly string StreamId1 = $"dc:{CodigoColaborador}:20260315";
    private static readonly string StreamId2 = $"dc:{CodigoColaborador}:20260316";

    protected override IPrivateEventHandlerAsync<EventoBus.DiasDepurados> Handler =>
        new DiasDepuradosEventHandler(EventStore);

    private static readonly ColaboradorBus ColaboradorRecibido =
        new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static EventoBus.DiaDepurado DiaRecibido(DateOnly fecha, string nombreTurno) => new(
        CodigoColaborador, fecha, ColaboradorRecibido, nombreTurno,
        [new EventoBus.FranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            fecha.ToDateTime(new TimeOnly(6, 0)), fecha.ToDateTime(new TimeOnly(14, 0)), false)],
        [new EventoBus.MarcacionDelDia(fecha.ToDateTime(new TimeOnly(6, 0)), "ENTRADA")],
        new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, []));

    private static DepuracionDiaRecibida FotoEsperada(string streamId, DateOnly fecha, string nombreTurno) => new(
        streamId, CodigoColaborador, fecha,
        new ResumenColaborador("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto"), nombreTurno,
        [new FranjaDepurada(
            new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            fecha.ToDateTime(new TimeOnly(6, 0)), fecha.ToDateTime(new TimeOnly(14, 0)), false)],
        [new MarcacionDelDia(fecha.ToDateTime(new TimeOnly(6, 0)), "ENTRADA")],
        new HorasDiscriminadas(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, []));

    private static DepuracionDiaRecibida FotoPrevia(string streamId, DateOnly fecha) => new(
        streamId, CodigoColaborador, fecha, null, null, [], [],
        new HorasDiscriminadas(new Dictionary<string, decimal>(), []));

    // CA-1
    [Fact]
    public async Task DiasDepurados_CreaLosDiasQueFaltanYAgregaFotoALosExistentes_CuandoElLoteMezclaAmbos()
    {
        Given(StreamId2, FotoPrevia(StreamId2, Fecha2));

        await WhenAsync(new EventoBus.DiasDepurados(
            CodigoColaborador, [DiaRecibido(Fecha1, "Turno Manana"), DiaRecibido(Fecha2, "Turno Tarde")]));

        Then(StreamId1, FotoEsperada(StreamId1, Fecha1, "Turno Manana"));
        Then(StreamId2, FotoEsperada(StreamId2, Fecha2, "Turno Tarde"));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId1, d => d.Estado, EstadoDiaCalculado.Provisional);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId2, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-2
    [Fact]
    public async Task DiasDepurados_RegistraEvidenciaPosAprobacionYAplicaElResto_CuandoUnDiaEstaAprobado()
    {
        Given(StreamId1,
            FotoPrevia(StreamId1, Fecha1),
            DiaAprobado.Crear(StreamId1, CodigoColaborador, Fecha1, []));

        await WhenAsync(new EventoBus.DiasDepurados(
            CodigoColaborador, [DiaRecibido(Fecha1, "Turno Manana"), DiaRecibido(Fecha2, "Turno Tarde")]));

        Then(StreamId1, new DepuracionPosAprobacionRecibida(
            StreamId1, CodigoColaborador, Fecha1,
            new ResumenColaborador("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto"), "Turno Manana",
            [new FranjaDepurada(
                new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                Fecha1.ToDateTime(new TimeOnly(6, 0)), Fecha1.ToDateTime(new TimeOnly(14, 0)), false)],
            [new MarcacionDelDia(Fecha1.ToDateTime(new TimeOnly(6, 0)), "ENTRADA")],
            new HorasDiscriminadas(new Dictionary<string, decimal> { ["OrdinariaDiurna"] = 8.00m }, [])));
        Then(StreamId2, FotoEsperada(StreamId2, Fecha2, "Turno Tarde"));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId1, d => d.Estado, EstadoDiaCalculado.Aprobado);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId2, d => d.Estado, EstadoDiaCalculado.Provisional);
    }

    // CA-3
    [Fact]
    public async Task DiasDepurados_RepiteLaMismaFoto_CuandoElLoteLlegaDosVeces()
    {
        Given(StreamId1, FotoEsperada(StreamId1, Fecha1, "Turno Manana"));

        await WhenAsync(new EventoBus.DiasDepurados(
            CodigoColaborador, [DiaRecibido(Fecha1, "Turno Manana")]));

        Then(StreamId1, FotoEsperada(StreamId1, Fecha1, "Turno Manana"));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId1, d => d.Estado, EstadoDiaCalculado.Provisional);
    }
}
