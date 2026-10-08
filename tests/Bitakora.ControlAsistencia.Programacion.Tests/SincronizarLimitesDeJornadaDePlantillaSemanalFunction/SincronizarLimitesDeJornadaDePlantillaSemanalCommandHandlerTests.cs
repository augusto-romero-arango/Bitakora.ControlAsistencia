using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarLimitesDeJornadaDePlantillaSemanalFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarLimitesDeJornadaDePlantillaSemanalFunction;

public class SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<SincronizarLimitesDeJornadaDePlantillaSemanal>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000883");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");
    private static readonly Guid OtraJornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a02");

    protected override ICommandHandlerAsync<SincronizarLimitesDeJornadaDePlantillaSemanal> Handler =>
        new SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler(EventStore);

    // 480 minutos ordinarios
    private static readonly Turno TurnoDeOchoHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    // 600 minutos ordinarios
    private static readonly Turno TurnoDeDiezHoras = Turno.Crear("Turno Largo", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0))]);

    private static LimitesJornada Limites(int semanales, int topeDiario) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(topeDiario, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaSemanalCreada PlantillaCreada() =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, "Semana Cocina", 1);

    private JornadaDePlantillaSemanalAsignada JornadaAsignada(long version = 1) =>
        JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(40, 8), version);

    private DiaDePlantillaSemanalAsignado Lunes(Turno turno) =>
        DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(1), TurnoId, turno, 1);

    private AdvertenciasDePlantillaSemanalCalculadas Advertencias(params AdvertenciaPlantillaSemanal[] advertencias) =>
        AdvertenciasDePlantillaSemanalCalculadas.Crear(GuidAggregateId, advertencias);

    private static AdvertenciaPlantillaSemanal[] SinTurno(params int[] dias) =>
        dias.Select(d => AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Desde(d))).ToArray();

    // Lunes de 600 min con tope de 8 h: supera el tope en 120; total 600 de 2400.
    private void GivenPlantillaConLunesQueSuperaElTope(long versionJornada = 1)
    {
        Given(PlantillaCreada(), JornadaAsignada(versionJornada), Lunes(TurnoDeDiezHoras),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1800),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Desde(1), 120),
                .. SinTurno(2, 3, 4, 5, 6, 7)]));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_EmiteSincronizadosYAdvertencias_CuandoLaVersionEsMayorYCambianLasAdvertencias()
    {
        GivenPlantillaConLunesQueSuperaElTope();
        var nuevos = Limites(40, 10);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, nuevos, 2));

        // Con tope de 10 h el lunes de 600 min deja de superarlo.
        AdvertenciaPlantillaSemanal[] esperadas =
        [
            AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1800),
            AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
            .. SinTurno(2, 3, 4, 5, 6, 7)
        ];
        Then(
            LimitesDeJornadaDePlantillaSemanalSincronizados.Crear(GuidAggregateId, JornadaId, nuevos, 2),
            Advertencias(esperadas));
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, nuevos);
        And<PlantillaSemanalTurnos, long>(p => p.VersionJornada, 2);
        And<PlantillaSemanalTurnos, bool>(p => p.Advertencias.SequenceEqual(esperadas), true);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_EmiteSoloSincronizados_CuandoLasAdvertenciasNoCambian()
    {
        Given(PlantillaCreada(), JornadaAsignada(), Lunes(TurnoDeOchoHoras),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1920),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                .. SinTurno(2, 3, 4, 5, 6, 7)]));
        var nuevos = Limites(40, 9);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, nuevos, 2));

        Then(LimitesDeJornadaDePlantillaSemanalSincronizados.Crear(GuidAggregateId, JornadaId, nuevos, 2));
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, nuevos);
        And<PlantillaSemanalTurnos, long>(p => p.VersionJornada, 2);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaVersionEsIgualALaVigente()
    {
        GivenPlantillaConLunesQueSuperaElTope(versionJornada: 3);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, Limites(40, 10), 3));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionJornada, 3);
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaVersionEsMenorALaVigente()
    {
        GivenPlantillaConLunesQueSuperaElTope(versionJornada: 5);

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, Limites(40, 10), 4));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionJornada, 5);
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaTieneOtraJornada()
    {
        GivenPlantillaConLunesQueSuperaElTope();

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, OtraJornadaId, Limites(40, 10), 9));

        Then();
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, JornadaId);
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaNoTieneJornada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), JornadaDePlantillaSemanalQuitada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, Limites(40, 10), 9));

        Then();
        And<PlantillaSemanalTurnos, Guid?>(p => p.JornadaId, null);
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, null);
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(), PlantillaSemanalRetirada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, Limites(40, 10), 2));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionJornada, 1);
        And<PlantillaSemanalTurnos, LimitesJornada?>(p => p.Limites, Limites(40, 8));
    }

    [Fact]
    public async Task SincronizarLimitesDeJornadaDePlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new SincronizarLimitesDeJornadaDePlantillaSemanal(GuidAggregateId, JornadaId, Limites(40, 10), 2));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SincronizarLimitesDeJornadaDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }
}
