using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Bitakora.ControlAsistencia.ControlHoras.RecibirDepuracionCuandoDiaDepurado.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using EventoBus = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.RecibirDepuracionCuandoDiaDepurado;

public class DiaDepuradoVersionEventHandlerTests : PrivateEventHandlerAsyncTest<EventoBus.DiaDepurado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly string StreamId = $"dc:{CodigoColaborador}:20260315";

    protected override IPrivateEventHandlerAsync<EventoBus.DiaDepurado> Handler =>
        new DiaDepuradoEventHandler(EventStore);

    private static EventoBus.DiaDepurado Foto(string nombreTurno, long? version) => new(
        CodigoColaborador, Fecha, null, nombreTurno, [], [],
        new EventoBus.HorasDiscriminadas(new Dictionary<string, decimal>(), []),
        VersionControlDiario: version);

    private static DepuracionDiaRecibida Persistida(string nombreTurno, long? version) => new(
        StreamId, CodigoColaborador, Fecha, null, nombreTurno, [], [],
        new HorasDiscriminadas(new Dictionary<string, decimal>(), []),
        versionControlDiario: version);

    private static string? TurnoDelDia(DiaCalculadoAggregateRoot dia) => dia.GenerarDepuracionDelDia().NombreTurno;

    [Fact]
    public async Task DiaDepurado_PasaLaVersionAlEventoPersistido_CuandoElDiaEsNuevo()
    {
        await WhenAsync(Foto("Turno V5", 5));

        Then(StreamId, Persistida("Turno V5", 5));
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno V5");
    }

    [Fact]
    public async Task DiaDepurado_NoEmiteEventos_CuandoLaFotoEsMasVieja()
    {
        Given(StreamId, Persistida("Turno V5", 5));

        await WhenAsync(Foto("Turno V3", 3));

        Then(StreamId);
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno V5");
    }

    [Fact]
    public async Task DiaDepurado_NoEmiteEventos_CuandoLaFotoTieneLaMismaVersion()
    {
        Given(StreamId, Persistida("Turno V5", 5));

        await WhenAsync(Foto("Turno V5 repetida", 5));

        Then(StreamId);
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno V5");
    }

    [Fact]
    public async Task DiaDepurado_ReemplazaLaFoto_CuandoLaFotoEsMasNueva()
    {
        Given(StreamId, Persistida("Turno V5", 5));

        await WhenAsync(Foto("Turno V6", 6));

        Then(StreamId, Persistida("Turno V6", 6));
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno V6");
    }

    [Fact]
    public async Task DiaDepurado_AplicaLaFoto_CuandoNoTraeVersion()
    {
        Given(StreamId, Persistida("Turno V5", 5));

        await WhenAsync(Foto("Turno sin version", null));

        Then(StreamId, Persistida("Turno sin version", null));
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno sin version");
    }

    [Fact]
    public async Task DiaDepurado_AplicaLaFotoVersionada_CuandoElDiaPrevioNoTieneVersion()
    {
        Given(StreamId, Persistida("Turno previo", null));

        await WhenAsync(Foto("Turno V1", 1));

        Then(StreamId, Persistida("Turno V1", 1));
        And<DiaCalculadoAggregateRoot, string?>(StreamId, TurnoDelDia, "Turno V1");
    }

    [Fact]
    public async Task DiaDepurado_NoEmiteEventos_CuandoElDiaEstaAprobadoYLaFotoEsMasVieja()
    {
        Given(StreamId,
            Persistida("Turno V5", 5),
            DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));

        await WhenAsync(Foto("Turno V3", 3));

        Then(StreamId);
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }

    [Fact]
    public async Task DiaDepurado_PersisteEvidenciaPosAprobacion_CuandoElDiaEstaAprobadoYLaFotoEsMasNueva()
    {
        Given(StreamId,
            Persistida("Turno V5", 5),
            DiaAprobado.Crear(StreamId, CodigoColaborador, Fecha, []));

        await WhenAsync(Foto("Turno V6", 6));

        Then(StreamId, new DepuracionPosAprobacionRecibida(
            StreamId, CodigoColaborador, Fecha, null, "Turno V6", [], [],
            new HorasDiscriminadas(new Dictionary<string, decimal>(), [])));
        And<DiaCalculadoAggregateRoot, EstadoDiaCalculado>(
            StreamId, d => d.Estado, EstadoDiaCalculado.Aprobado);
    }
}
