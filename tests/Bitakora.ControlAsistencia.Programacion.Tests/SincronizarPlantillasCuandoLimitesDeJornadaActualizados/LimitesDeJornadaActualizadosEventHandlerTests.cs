// La reaccion no persiste eventos ni tiene aggregate: consulta el cuadro y envia comandos. Por eso
// no usa Then()/And<>() del harness; el efecto observable son los comandos que recibe el router.

using AwesomeAssertions;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoLimitesDeJornadaActualizados.EventHandler;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarPlantillasCuandoLimitesDeJornadaActualizados;

public class LimitesDeJornadaActualizadosEventHandlerTests : PrivateEventHandlerAsyncTest<LimitesDeJornadaActualizados>
{
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid PlantillaA = Guid.Parse("019600a0-0000-7000-8000-0000000000a1");
    private static readonly Guid PlantillaB = Guid.Parse("019600a0-0000-7000-8000-0000000000b2");

    private readonly FakeLectorPlantillasPorJornada _lector = new();
    private readonly FakeRouterRegistrador _router = new();

    protected override IPrivateEventHandlerAsync<LimitesDeJornadaActualizados> Handler =>
        new LimitesDeJornadaActualizadosEventHandler(_lector, _router);

    // 42 h semanales, tope 9 h, minimo 2 h 30, 2 descansos.
    private static LimitesDeJornadaActualizados Evento(long version = 4) =>
        new(JornadaId, version, 2520, 540, 150, 2);

    private static LimitesJornada LimitesEsperados() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(2, 30), 2);

    [Fact]
    public async Task LimitesDeJornadaActualizados_EnviaElComandoACadaPlantillaDeLaJornada_CuandoCambianLosLimites()
    {
        _lector.PlantillaIds = [PlantillaA.ToString(), PlantillaB.ToString()];

        await WhenAsync(Evento());

        _lector.JornadasConsultadas.Should().Equal(JornadaId);
        var esperados = LimitesEsperados();
        _router.Comandos.Should().Equal(
            new SincronizarLimitesDeJornadaDePlantillaSemanal(PlantillaA, JornadaId, esperados, 4),
            new SincronizarLimitesDeJornadaDePlantillaSemanal(PlantillaB, JornadaId, esperados, 4));
    }

    [Fact]
    public async Task LimitesDeJornadaActualizados_NoEnviaComandos_CuandoNingunaPlantillaUsaLaJornada()
    {
        _lector.PlantillaIds = [];

        await WhenAsync(Evento());

        _lector.JornadasConsultadas.Should().Equal(JornadaId);
        _router.Comandos.Should().BeEmpty();
    }

    [Fact]
    public async Task LimitesDeJornadaActualizados_Lanza_CuandoLosDatosNoSePuedenReinstanciar()
    {
        _lector.PlantillaIds = [PlantillaA.ToString()];
        var invalido = new LimitesDeJornadaActualizados(JornadaId, 3, 2520, 540, 150, 7);

        var act = async () => await WhenAsync(invalido);

        await act.Should().ThrowAsync<Exception>();
        _router.Comandos.Should().BeEmpty();
    }

    private sealed class FakeLectorPlantillasPorJornada : ILectorPlantillasPorJornada
    {
        public IReadOnlyList<string> PlantillaIds { get; set; } = [];
        public List<Guid> JornadasConsultadas { get; } = [];

        public Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid jornadaId, CancellationToken ct)
        {
            JornadasConsultadas.Add(jornadaId);
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
