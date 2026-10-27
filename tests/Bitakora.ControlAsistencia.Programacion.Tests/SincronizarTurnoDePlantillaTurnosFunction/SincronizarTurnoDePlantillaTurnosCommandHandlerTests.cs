using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaTurnosFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarTurnoDePlantillaTurnosFunction;

public class SincronizarTurnoDePlantillaTurnosCommandHandlerTests
    : CommandHandlerAsyncTest<SincronizarTurnoDePlantillaTurnos>
{
    private const int DiasDelCiclo = 4;
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001285");
    private static readonly Guid OtroTurnoId = Guid.Parse("019600a0-0000-7000-8000-000000001286");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000001a01");

    protected override ICommandHandlerAsync<SincronizarTurnoDePlantillaTurnos> Handler =>
        new SincronizarTurnoDePlantillaTurnosCommandHandler(EventStore);

    // 480 minutos ordinarios
    private static readonly Turno TurnoDeOchoHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    // 600 minutos ordinarios
    private static readonly Turno TurnoDeDiezHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0))]);

    private static readonly Turno TurnoOtro = Turno.Crear("Turno Tarde", false,
        [FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0))]);

    // Tope diario 8 h, minimo 0.
    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(40, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaDeTurnosCreada PlantillaCreada() =>
        PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Cocina", DiasDelCiclo);

    private JornadaDePlantillaDeTurnosAsignada JornadaAsignada() =>
        JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(), 1);

    private DiaDePlantillaDeTurnosAsignado Dia(int dia, Guid turnoId, Turno turno, long version) =>
        DiaDePlantillaDeTurnosAsignado.Crear(GuidAggregateId, dia, turnoId, turno, version);

    private AdvertenciasDePlantillaDeTurnosCalculadas Advertencias(params AdvertenciaPlantillaTurnos[] advertencias) =>
        AdvertenciasDePlantillaDeTurnosCalculadas.Crear(GuidAggregateId, advertencias);

    private static AdvertenciaPlantillaTurnos[] SinTurno(params int[] dias) =>
        dias.Select(AdvertenciaPlantillaTurnos.DiaSinTurno).ToArray();

    private void GivenPlantillaConTurnoEnDosDias(long version = 1) =>
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, version),
            Dia(2, TurnoId, TurnoDeOchoHoras, version),
            Advertencias(SinTurno(3, 4)));

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_EmiteSincronizadoYAdvertencias_CuandoLaVersionEsMayorYCambianLasAdvertencias()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        AdvertenciaPlantillaTurnos[] esperadas =
        [
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120),
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(2, 120),
            .. SinTurno(3, 4)
        ];
        Then(
            TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false),
            Advertencias(esperadas));
        And<PlantillaTurnos, bool>(p => p.Advertencias.SequenceEqual(esperadas), true);
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 2);
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), false);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_EmiteSoloSincronizado_CuandoLasAdvertenciasNoCambian()
    {
        GivenPlantillaConTurnoEnDosDias();
        var otroNombre = Turno.Crear("Turno Manana Renombrado", false,
            [FranjaOrdinaria.Crear(new TimeOnly(7, 0), new TimeOnly(15, 0))]);

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, otroNombre, 2, false));

        Then(TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, otroNombre, 2, false));
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 2);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoToca_LosDiasDeOtroTurno()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            Dia(2, OtroTurnoId, TurnoOtro, 1),
            Advertencias(SinTurno(3, 4)));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        AdvertenciaPlantillaTurnos[] esperadas =
        [
            AdvertenciaPlantillaTurnos.SuperaTopeDiario(1, 120),
            .. SinTurno(3, 4)
        ];
        Then(
            TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false),
            Advertencias(esperadas));
        And<PlantillaTurnos, bool>(p => p.Advertencias.SequenceEqual(esperadas), true);
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(OtroTurnoId), 1);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsIgualALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 3);

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 3, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 3);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionEsMenorALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 5);

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 4, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 5);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoNingunDiaUsaElTurno()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, OtroTurnoId, TurnoOtro, 9, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(OtroTurnoId), 0);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_MarcaLosDiasYLosReportaSinTurno_CuandoElTurnoVieneRetirado()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            Advertencias(SinTurno(2, 3, 4)));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        var esperadas = SinTurno(1, 2, 3, 4);
        Then(
            TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true),
            Advertencias(esperadas));
        And<PlantillaTurnos, bool>(p => p.Advertencias.SequenceEqual(esperadas), true);
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaVersionYaFueSincronizadaComoRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            TurnoDePlantillaDeTurnosSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true),
            Advertencias(SinTurno(1, 2, 3, 4)));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        Then();
        And<PlantillaTurnos, bool>(p => p.DiaEstaRetirado(1), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_NoEmiteEventos_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            PlantillaDeTurnosRetirada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then();
        And<PlantillaTurnos, long>(p => p.VersionDelTurno(TurnoId), 1);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new SincronizarTurnoDePlantillaTurnos(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SincronizarTurnoDePlantillaTurnosCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }
}
