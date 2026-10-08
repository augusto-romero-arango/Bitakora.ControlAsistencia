// La reaccion no persiste eventos ni tiene aggregate: consulta el cuadro y envia comandos. Por eso
// no usa Then()/And<>() del harness; el efecto observable son los comandos que recibe el router.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado.EventHandler;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

public class DisenoDeTurnoActualizadoEventHandlerTests : PrivateEventHandlerAsyncTest<DisenoDeTurnoActualizado>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000883");
    private static readonly Guid PlantillaA = Guid.Parse("019600a0-0000-7000-8000-0000000000a1");
    private static readonly Guid PlantillaB = Guid.Parse("019600a0-0000-7000-8000-0000000000b2");

    private readonly FakeLectorPlantillasPorTurno _lector = new();
    private readonly FakeRouterRegistrador _router = new();

    protected override IPrivateEventHandlerAsync<DisenoDeTurnoActualizado> Handler =>
        new DisenoDeTurnoActualizadoEventHandler(_lector, _router);

    private static DisenoDeTurnoActualizado DisenoMixto(long version = 4, bool retirado = false) => new(
        TurnoId, version, "Turno Mixto", false,
        [
            new DetalleFranjaOrdinaria(
                new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
                [new DetalleSubFranja(new TimeOnly(9, 0), new TimeOnly(9, 30), 0, 0, "09:00-09:30")],
                [new DetalleSubFranja(new TimeOnly(13, 0), new TimeOnly(14, 0), 0, 0, "13:00-14:00")],
                "06:00-14:00",
                new DetalleSede("s:001", "Sede Principal", "CC-100")),
            new DetalleFranjaOrdinaria(
                new TimeOnly(22, 0), new TimeOnly(5, 0), 1, [], [], "22:00-05:00")
        ],
        retirado);

    private static Turno TurnoMixtoEsperado() => Turno.Crear("Turno Mixto", false,
    [
        FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0), 0,
            [SubFranja.Crear(new TimeOnly(9, 0), new TimeOnly(9, 30))],
            [SubFranja.Crear(new TimeOnly(13, 0), new TimeOnly(14, 0))],
            new SedeProgramada("s:001", "Sede Principal", "CC-100")),
        FranjaOrdinaria.Crear(new TimeOnly(22, 0), new TimeOnly(5, 0), 1)
    ]);

    [Fact]
    public async Task DisenoDeTurnoActualizado_EnviaElComandoACadaPlantillaQueUsaElTurno_CuandoElDisenoCambia()
    {
        _lector.PlantillaIds = [PlantillaA.ToString(), PlantillaB.ToString()];

        await WhenAsync(DisenoMixto());

        _lector.TurnosConsultados.Should().Equal(TurnoId);
        var esperado = TurnoMixtoEsperado();
        _router.Comandos.Should().Equal(
            new SincronizarTurnoDePlantillaSemanal(PlantillaA, TurnoId, esperado, 4, false),
            new SincronizarTurnoDePlantillaSemanal(PlantillaB, TurnoId, esperado, 4, false));
    }

    [Fact]
    public async Task DisenoDeTurnoActualizado_NoEnviaComandos_CuandoNingunaPlantillaUsaElTurno()
    {
        _lector.PlantillaIds = [];

        await WhenAsync(DisenoMixto());

        _lector.TurnosConsultados.Should().Equal(TurnoId);
        _router.Comandos.Should().BeEmpty();
    }

    [Fact]
    public async Task DisenoDeTurnoActualizado_EnviaLaCopiaMarcadaRetirada_CuandoElTurnoVieneRetirado()
    {
        _lector.PlantillaIds = [PlantillaA.ToString()];

        await WhenAsync(DisenoMixto(version: 9, retirado: true));

        _router.Comandos.Should().Equal(
            new SincronizarTurnoDePlantillaSemanal(PlantillaA, TurnoId, TurnoMixtoEsperado(), 9, true));
    }

    [Fact]
    public async Task DisenoDeTurnoActualizado_ReinstanciaElTurnoDeDescanso_CuandoNoTraeFranjas()
    {
        _lector.PlantillaIds = [PlantillaA.ToString()];

        await WhenAsync(new DisenoDeTurnoActualizado(TurnoId, 2, "Descanso Compensatorio", true, [], false));

        _router.Comandos.Should().Equal(new SincronizarTurnoDePlantillaSemanal(
            PlantillaA, TurnoId, Turno.Crear("Descanso Compensatorio", true, []), 2, false));
    }

    [Fact]
    public async Task DisenoDeTurnoActualizado_Lanza_CuandoLosDatosNoSePuedenReinstanciar()
    {
        _lector.PlantillaIds = [PlantillaA.ToString()];
        var invalido = new DisenoDeTurnoActualizado(
            TurnoId, 3, "Turno Roto", false,
            [new DetalleFranjaOrdinaria(new TimeOnly(10, 0), new TimeOnly(10, 0), 0, [], [], "10:00-10:00")],
            false);

        var act = async () => await WhenAsync(invalido);

        await act.Should().ThrowExactlyAsync<ArgumentException>();
        _router.Comandos.Should().BeEmpty();
    }

    private sealed class FakeLectorPlantillasPorTurno : ILectorPlantillasPorTurno
    {
        public IReadOnlyList<string> PlantillaIds { get; set; } = [];
        public List<Guid> TurnosConsultados { get; } = [];

        public Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid turnoId, CancellationToken ct)
        {
            TurnosConsultados.Add(turnoId);
            return Task.FromResult(PlantillaIds);
        }
    }

    private sealed class FakeRouterRegistrador : ICommandRouter
    {
        public List<object> Comandos { get; } = [];

        public Task InvokeAsync<TCommand>(TCommand command, CancellationToken ct = default)
            where TCommand : class
        {
            Comandos.Add(command);
            return Task.CompletedTask;
        }

        public Task<TResult> InvokeAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default)
            where TCommand : class =>
            throw new NotImplementedException();
    }
}
