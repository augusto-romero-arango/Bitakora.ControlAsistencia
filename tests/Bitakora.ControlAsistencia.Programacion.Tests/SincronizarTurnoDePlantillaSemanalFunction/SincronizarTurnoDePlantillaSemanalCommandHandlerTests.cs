using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.SincronizarTurnoDePlantillaSemanalFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.SincronizarTurnoDePlantillaSemanalFunction;

public class SincronizarTurnoDePlantillaSemanalCommandHandlerTests
    : CommandHandlerAsyncTest<SincronizarTurnoDePlantillaSemanal>
{
    private static readonly Guid TurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000883");
    private static readonly Guid OtroTurnoId = Guid.Parse("019600a0-0000-7000-8000-000000000884");
    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000a01");

    protected override ICommandHandlerAsync<SincronizarTurnoDePlantillaSemanal> Handler =>
        new SincronizarTurnoDePlantillaSemanalCommandHandler(EventStore);

    // 480 minutos ordinarios
    private static readonly Turno TurnoDeOchoHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(14, 0))]);

    // 600 minutos ordinarios
    private static readonly Turno TurnoDeDiezHoras = Turno.Crear("Turno Manana", false,
        [FranjaOrdinaria.Crear(new TimeOnly(6, 0), new TimeOnly(16, 0))]);

    private static readonly Turno TurnoOtro = Turno.Crear("Turno Tarde", false,
        [FranjaOrdinaria.Crear(new TimeOnly(14, 0), new TimeOnly(22, 0))]);

    // Tope diario 8 h, minimo 0, 1 dia de descanso por semana.
    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private PlantillaSemanalCreada PlantillaCreada() =>
        PlantillaSemanalCreada.Crear(GuidAggregateId, "Semana Cocina", 1);

    private JornadaDePlantillaSemanalAsignada JornadaAsignada() =>
        JornadaDePlantillaSemanalAsignada.Crear(GuidAggregateId, JornadaId, Limites(40), 1);

    private DiaDePlantillaSemanalAsignado Dia(int dia, Guid turnoId, Turno turno, long version) =>
        DiaDePlantillaSemanalAsignado.Crear(GuidAggregateId, 1, DiaSemana.Desde(dia), turnoId, turno, version);

    private AdvertenciasDePlantillaSemanalCalculadas Advertencias(params AdvertenciaPlantillaSemanal[] advertencias) =>
        AdvertenciasDePlantillaSemanalCalculadas.Crear(GuidAggregateId, advertencias);

    private static AdvertenciaPlantillaSemanal[] SinTurno(params int[] dias) =>
        dias.Select(d => AdvertenciaPlantillaSemanal.DiaSinTurno(1, DiaSemana.Desde(d))).ToArray();

    // Lunes y martes con el mismo turno de 480 min: total 960 de 2400, sin descanso.
    private void GivenPlantillaConTurnoEnDosDias(long version = 1)
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, version),
            Dia(2, TurnoId, TurnoDeOchoHoras, version),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1440),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                .. SinTurno(3, 4, 5, 6, 7)]));
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_EmiteSincronizadoYAdvertencias_CuandoLaVersionEsMayorYCambianLasAdvertencias()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        // 600 min supera el tope de 480 en 120 los dos dias; total 1200 de 2400.
        Then(
            TurnoDePlantillaSemanalSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1200),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Desde(1), 120),
                AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Desde(2), 120),
                .. SinTurno(3, 4, 5, 6, 7)]));
        And<PlantillaSemanalTurnos, int>(p => p.CopiasDelTurno(TurnoId).Count, 2);
        And<PlantillaSemanalTurnos, bool>(p => p.CopiasDelTurno(TurnoId).All(c => c.Equals(TurnoDeDiezHoras)), true);
        And<PlantillaSemanalTurnos, long>(p => p.VersionDelTurno(TurnoId), 2);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_EmiteSoloSincronizado_CuandoLasAdvertenciasNoCambian()
    {
        GivenPlantillaConTurnoEnDosDias();
        var otroNombre = Turno.Crear("Turno Manana Renombrado", false,
            [FranjaOrdinaria.Crear(new TimeOnly(7, 0), new TimeOnly(15, 0))]);

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, otroNombre, 2, false));

        Then(TurnoDePlantillaSemanalSincronizado.Crear(GuidAggregateId, TurnoId, otroNombre, 2, false));
        And<PlantillaSemanalTurnos, bool>(p => p.CopiasDelTurno(TurnoId).All(c => c.Equals(otroNombre)), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_NoToca_LosDiasDeOtroTurno()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            Dia(2, OtroTurnoId, TurnoOtro, 1),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1440),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                .. SinTurno(3, 4, 5, 6, 7)]));

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then(
            TurnoDePlantillaSemanalSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1320),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                AdvertenciaPlantillaSemanal.SuperaTopeDiario(1, DiaSemana.Desde(1), 120),
                .. SinTurno(3, 4, 5, 6, 7)]));
        And<PlantillaSemanalTurnos, bool>(p => p.CopiasDelTurno(OtroTurnoId).All(c => c.Equals(TurnoOtro)), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_NoEmiteEventos_CuandoLaVersionEsIgualALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 3);

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 3, false));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionDelTurno(TurnoId), 3);
        And<PlantillaSemanalTurnos, bool>(p => p.CopiasDelTurno(TurnoId).All(c => c.Equals(TurnoDeOchoHoras)), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_NoEmiteEventos_CuandoLaVersionEsMenorALaVigente()
    {
        GivenPlantillaConTurnoEnDosDias(version: 5);

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 4, false));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionDelTurno(TurnoId), 5);
        And<PlantillaSemanalTurnos, bool>(p => p.CopiasDelTurno(TurnoId).All(c => c.Equals(TurnoDeOchoHoras)), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaNoTieneDiasConEseTurno()
    {
        GivenPlantillaConTurnoEnDosDias();

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, OtroTurnoId, TurnoOtro, 9, false));

        Then();
        And<PlantillaSemanalTurnos, int>(p => p.CopiasDelTurno(OtroTurnoId).Count, 0);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_MarcaLosDiasYLosReportaSinTurno_CuandoElTurnoVieneRetirado()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 1920),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                .. SinTurno(2, 3, 4, 5, 6, 7)]));

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true));

        Then(
            TurnoDePlantillaSemanalSincronizado.Crear(GuidAggregateId, TurnoId, TurnoDeOchoHoras, 2, true),
            Advertencias([
                AdvertenciaPlantillaSemanal.PorDebajoDeHorasSemanales(1, 2400),
                AdvertenciaPlantillaSemanal.FaltanDiasDeDescanso(1, 1),
                .. SinTurno(1, 2, 3, 4, 5, 6, 7)]));
        And<PlantillaSemanalTurnos, bool>(p => p.TurnoEstaRetirado(TurnoId), true);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_NoEmiteEventos_CuandoLaPlantillaEstaRetirada()
    {
        Given(PlantillaCreada(), JornadaAsignada(),
            Dia(1, TurnoId, TurnoDeOchoHoras, 1),
            PlantillaSemanalRetirada.Crear(GuidAggregateId));

        await WhenAsync(new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        Then();
        And<PlantillaSemanalTurnos, long>(p => p.VersionDelTurno(TurnoId), 1);
    }

    [Fact]
    public async Task SincronizarTurnoDePlantillaSemanal_LanzaRecursoNoEncontradoException_CuandoLaPlantillaNoExiste()
    {
        var act = async () => await WhenAsync(
            new SincronizarTurnoDePlantillaSemanal(GuidAggregateId, TurnoId, TurnoDeDiezHoras, 2, false));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{SincronizarTurnoDePlantillaSemanalCommandHandler.Mensajes.PlantillaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }
}
