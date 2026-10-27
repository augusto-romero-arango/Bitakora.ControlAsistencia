using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaTurnosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarLimitesDeJornadaDePlantillaTurnosFunction;

public class SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<SincronizarLimitesDeJornadaDePlantillaTurnos>
{
    private const int DiasDelCiclo = 4;
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001296");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid OtraJornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    protected override ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaTurnos> Handler =>
        new SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler(EventStore);

    private static readonly Turno TurnoDeOchoHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    private static readonly Turno TurnoDeDiezHoras = Turno.Crear("Turno Largo", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0))]);

    private static LimitesJornada Limites(int semanales, int topeDiario) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(topeDiario, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaDeTurnosCreada PlantillaCreada() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Cocina", DiasDelCiclo);

    private JornadaDePlantillaDeTurnosAsignada JornadaAsignada(long version = 1) =>
        JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(40, 8), version);

    private DiaDePlantillaDeTurnosAsignado Dia(int dia, Turno turno) =>
        DiaDePlantillaDeTurnosAsignado.Crear(GuidAggregateId, dia, TurnoId, turno, 1);

    private AdvertenciasDePlantillaDeTurnosCalculadas Advertencias(params AdvertenciaPlantillaTurnos[] advertencias) =>
        AdvertenciasDePlantillaDeTurnosCalculadas.Crear(GuidAggregateId, advertencias);

    private static AdvertenciaPlantillaTurnos[] SinTurno(params int[] dias) =>
        dias.Select(AdvertenciaPlantillaTurnos.DiaSinTurno).ToArray();

    // Dias 1 y 2 de 600 min con tope de 8 h: superan el tope en 120.
    private void GivenPlantillaConDiasQueSuperanElTope(long versionJornada = 1) =>
        Given(PlantillaCreada(), JornadaAsignada(versionJornada),
            Dia(1, TurnoDeDiezHoras), Dia(2, TurnoDeDiezHoras),
            Advertencias([
                AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120),
                AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 120),
                .. SinTurno(3, 4)]));

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_EmiteSincronizadosYAdvertencias_CuandoLaVersionEsMayorYCambianLasAdvertencias()
    {
        GivenPlantillaConDiasQueSuperanElTope();
        var nuevos = Limites(40, 10);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, nuevos, 2));

        AdvertenciaPlantillaTurnos[] esperadas = [.. SinTurno(3, 4)];
        Then(
            LimitesDeJornadaDePlantillaDeTurnosSincronizados.Crear(GuidAggregateId, JornadaId, nuevos, 2),
            Advertencias(esperadas));
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, nuevos);
        And<PlantillaTurnos, long>(p => p.VersionJornada, 2);
        And<PlantillaTurnos, bool>(p => p.Advertencias.SequenceEqual(esperadas), true);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_EmiteSoloSincronizados_CuandoLasAdvertenciasNoCambian()
    {
        Given(PlantillaCreada(), JornadaAsignada(), Dia(1, TurnoDeOchoHoras), Dia(2, TurnoDeOchoHoras),
            Advertencias(SinTurno(3, 4)));
        var nuevos = Limites(40, 9);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, nuevos, 2));

        Then(LimitesDeJornadaDePlantillaDeTurnosSincronizados.Crear(GuidAggregateId, JornadaId, nuevos, 2));
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, nuevos);
        And<PlantillaTurnos, long>(p => p.VersionJornada, 2);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsIgualALaVigente()
    {
        GivenPlantillaConDiasQueSuperanElTope(versionJornada: 3);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, Limites(40, 10), 3));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionJornada, 3);
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsMenorALaVigente()
    {
        GivenPlantillaConDiasQueSuperanElTope(versionJornada: 5);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, Limites(40, 10), 4));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionJornada, 5);
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaTieneOtraJornada()
    {
        GivenPlantillaConDiasQueSuperanElTope();

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, OtraJornadaId, Limites(40, 10), 9));

        Then();
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), JornadaDePlantillaDeTurnosQuitada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, Limites(40, 10), 9));

        Then();
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, null);
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, null);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), PlantillaDeTurnosRetirada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, Limites(40, 10), 2));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionJornada, 1);
        And<PlantillaTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new SincronizarLimitesDeJornadaDePlantillaTurnos(GuidAggregateId, JornadaId, Limites(40, 10), 2));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SincronizarLimitesDeJornadaDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }
}
