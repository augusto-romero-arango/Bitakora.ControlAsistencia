using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoRegistroDeMarcacionCreado.EventHandler;
using Bitakora.ControlAsistencia.ControlHoras.DomainEvents;
using Bitakora.ControlAsistencia.ControlHoras.Entities;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Testing.Utilities;
using RegistroDeMarcacionCreado = Bitakora.ControlAsistencia.PrivateEvents.ControlHoras.RegistroDeMarcacionCreado;

namespace Bitakora.ControlAsistencia.ControlHoras.Tests.Entities;

public class VersionControlDiarioTests : PrivateEventHandlerAsyncTest<RegistroDeMarcacionCreado>
{
    private const string CodigoColaborador = "EMP-001";
    private static readonly string StreamId = "cd:EMP-001:20260315";
    private static readonly DateTime Timestamp07_00 = new(2026, 3, 15, 7, 0, 0);
    private static readonly DateTime Timestamp12_00 = new(2026, 3, 15, 12, 0, 0);
    private static readonly DateTime Timestamp15_00 = new(2026, 3, 15, 15, 0, 0);

    protected override IPrivateEventHandlerAsync<RegistroDeMarcacionCreado> Handler =>
        new RegistroDeMarcacionCreadoEventHandler(EventStore, PrivateEventSender);

    private static MarcacionAdicionada Marcacion(DateTime timestamp) =>
        new(StreamId, CodigoColaborador, timestamp, "ENTRADA", "DEV-001");

    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private static readonly ColaboradorProgramado Colaborador = new("CC-1234567890", CodigoColaborador, "Luis Augusto Barreto");

    private static TurnoDiarioAsignado Turno() =>
        new(StreamId,
            Colaborador,
            Fecha,
            new TurnoDiario("Turno Manana",
                [new FranjaProgramada(new TimeOnly(6, 0), new TimeOnly(14, 0), 0, [], [], "")], ""),
            Guid.Parse("019600d0-0000-7000-8000-000000000001"));

    [Fact]
    public async Task CrearDiaDepurado_LlevaVersionUno_CuandoSoloSeEmitioLaPrimeraMarcacion()
    {
        await WhenAsync(new RegistroDeMarcacionCreado(CodigoColaborador, Timestamp07_00, "ENTRADA", "DEV-001"));

        Then(StreamId, Marcacion(Timestamp07_00));
        And<ControlDiarioAggregateRoot, long?>(StreamId, c => c.CrearDiaDepurado().VersionControlDiario, 1L);
    }

    [Fact]
    public async Task CrearDiaDepurado_CuentaEventosRehidratadosYEmitidos_CuandoHayHistorial()
    {
        Given(StreamId, Marcacion(Timestamp07_00), Marcacion(Timestamp12_00));

        await WhenAsync(new RegistroDeMarcacionCreado(CodigoColaborador, Timestamp15_00, "ENTRADA", "DEV-001"));

        Then(StreamId, Marcacion(Timestamp15_00));
        And<ControlDiarioAggregateRoot, long?>(StreamId, c => c.CrearDiaDepurado().VersionControlDiario, 3L);
    }

    [Fact]
    public async Task CrearDiaDepurado_CuentaEventosDeTiposDistintos_CuandoElHistorialMezclaTurnoYMarcaciones()
    {
        Given(StreamId, Turno(), Marcacion(Timestamp07_00));

        await WhenAsync(new RegistroDeMarcacionCreado(CodigoColaborador, Timestamp15_00, "ENTRADA", "DEV-001"));

        Then(StreamId, Marcacion(Timestamp15_00));
        And<ControlDiarioAggregateRoot, long?>(StreamId, c => c.CrearDiaDepurado().VersionControlDiario, 3L);
    }

    // Un Apply que olvide el contador rompe la monotonia: el historial trae los diez tipos de evento.
    [Fact]
    public async Task CrearDiaDepurado_CuentaCadaTipoDeEvento_CuandoElHistorialTraeTodosLosTiposDelControlDiario()
    {
        var ausenciaId = Guid.Parse("019600d0-0000-7000-8000-000000000002");
        Given(StreamId,
            Turno(),
            Marcacion(Timestamp07_00),
            new SedeDeMarcacionIdentificada(StreamId, Timestamp07_00, "DEV-001", "001", "Sede Principal", null),
            CoberturaVecinaActualizada.Crear(StreamId, DiaVecino.Vispera, null),
            CierreDeLaVisperaActualizado.Crear(StreamId, null),
            AmbiguedadDeMarcacionesResuelta.Crear(StreamId, DiaVecino.Vispera, []),
            ResolucionDeAmbiguedadDescartada.Crear(StreamId, DiaVecino.Vispera),
            TurnoDiarioCancelado.Crear(StreamId, Colaborador, Fecha, Guid.Parse("019600d0-0000-7000-8000-000000000003")),
            AusenciaDiariaAsignada.Crear(StreamId, Colaborador, Fecha, ausenciaId, "Vacaciones"),
            CancelacionAusenciaDiariaRegistrada.Crear(StreamId, ausenciaId, Fecha));

        await WhenAsync(new RegistroDeMarcacionCreado(CodigoColaborador, Timestamp15_00, "ENTRADA", "DEV-001"));

        Then(StreamId, Marcacion(Timestamp15_00));
        And<ControlDiarioAggregateRoot, long?>(StreamId, c => c.CrearDiaDepurado().VersionControlDiario, 11L);
    }
}
