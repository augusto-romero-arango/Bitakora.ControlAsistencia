using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;
using static Bitakora.ControlAsistencia.Programacion.Tests.Entities.AdvertenciasEsperadasPlantillaTurnos;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearPlantillaTurnosFunction;

public class CrearPlantillaTurnosCommandHandlerTests : CommandHandlerAsyncTest<CrearPlantillaTurnos>
{
    private const string NombrePlantilla = "Ciclo 8 dias";

    private FakeLectorNombresPlantillaTurnos _lector = new();

    protected override ICommandHandlerAsync<CrearPlantillaTurnos> Handler =>
        new CrearPlantillaTurnosCommandHandler(EventStore, _lector);

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreadaYEstableceId_CuandoPlantillaNoExiste()
    {
        Given();
        await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8),
            EventoSinJornada(GuidAggregateId));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreada_CuandoDiasDelCicloEsElMaximo()
    {
        Given();
        await WhenAsync(new CrearPlantillaTurnos(
            GuidAggregateId, NombrePlantilla, PlantillaDeTurnosCreada.MaximoDiasDelCiclo));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 42),
            EventoSinJornada(GuidAggregateId));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaRecursoYaExisteException_CuandoPlantillaYaExiste()
    {
        Given(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8));

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaAggregateException_CuandoDiasDelCicloEstaFueraDeRango()
    {
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 0));

        await act.Should().ThrowExactlyAsync<AggregateException>();
        Then();
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    private static readonly Guid JornadaId = Guid.Parse("019600a0-0000-7000-8000-000000000b02");

    private const long VersionEnElHarness = 0;

    private static LimitesJornada Limites(int semanales) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1);

    [Fact]
    public async Task CrearPlantillaTurnos_EmiteCreadaJornadaAsignadaYUnaAuditoria_CuandoLaJornadaExiste()
    {
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(42)));

        await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8, JornadaId));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8),
            JornadaDePlantillaDeTurnosAsignada.Crear(GuidAggregateId, JornadaId, Limites(42), VersionEnElHarness),
            EventoDiasSinTurno(GuidAggregateId, 8));
        And<PlantillaTurnos, Guid?>(p => p.JornadaId, JornadaId);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaRecursoNoEncontradoException_CuandoLaJornadaNoTieneStream()
    {
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoLaJornadaEstaDesactivada()
    {
        Given(JornadaId.ToString(), JornadaCreada.Crear(JornadaId, Limites(42)), new JornadaDesactivada(JornadaId));

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8, JornadaId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.JornadaDesactivada}*");
        Then(GuidAggregateId.ToString());
        And<Jornada, bool>(JornadaId.ToString(), j => j.Activa, false);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaPlantillaYaExiste_CuandoPlantillaYaExisteYLaJornadaNoExiste()
    {
        Given(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8));

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8, JornadaId));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoElNombreCoincideConUnoVigente()
    {
        _lector = new FakeLectorNombresPlantillaTurnos("Ciclo Noche");
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, "Ciclo Noche", 8));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaReglaDeNegocioDeclinadaException_CuandoElNombreDifiereSoloEnMayusculasYEspacios()
    {
        _lector = new FakeLectorNombresPlantillaTurnos("Ciclo Noche");
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, "  ciclo   NOCHE ", 8));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreada_CuandoElNombreDifiereSoloEnAcentos()
    {
        _lector = new FakeLectorNombresPlantillaTurnos("Ciclo Noche");
        Given();

        await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, "Ciclo Noché", 8));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, "Ciclo Noché", 8),
            EventoSinJornada(GuidAggregateId));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_EmitePlantillaDeTurnosCreada_CuandoNingunNombreVigenteCoincide()
    {
        _lector = new FakeLectorNombresPlantillaTurnos("Ciclo Dia", "Ciclo Tarde");
        Given();

        await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        Then(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8),
            EventoSinJornada(GuidAggregateId));
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaPlantillaYaExiste_CuandoPlantillaYaExisteYElNombreEstaDuplicado()
    {
        Given(PlantillaDeTurnosCreada.Crear(GuidAggregateId, NombrePlantilla, 8));
        _lector = new FakeLectorNombresPlantillaTurnos(NombrePlantilla);

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8));

        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.PlantillaYaExiste}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaNombreDuplicado_CuandoNombreEstaDuplicadoYDiasDelCicloEsInvalido()
    {
        _lector = new FakeLectorNombresPlantillaTurnos(NombrePlantilla);
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 0));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    [Fact]
    public async Task CrearPlantillaTurnos_LanzaNombreDuplicado_CuandoNombreEstaDuplicadoYLaJornadaNoExiste()
    {
        _lector = new FakeLectorNombresPlantillaTurnos(NombrePlantilla);
        Given();

        var act = async () => await WhenAsync(new CrearPlantillaTurnos(GuidAggregateId, NombrePlantilla, 8, JornadaId));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{CrearPlantillaTurnosCommandHandler.Mensajes.NombreDuplicado}*");
        Then(GuidAggregateId.ToString());
        And<PlantillaTurnos, string>(p => p.Id, string.Empty);
    }

    [Fact]
    public void Mensajes_JornadaNoEncontradaYDesactivadaTienenTextoEnElResx_CuandoSeConsultaPorResourceManager()
    {
        CrearPlantillaTurnosCommandHandler.Mensajes.JornadaNoEncontrada.Should().NotBeNullOrWhiteSpace();
        CrearPlantillaTurnosCommandHandler.Mensajes.JornadaDesactivada.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Mensajes_NombreDuplicadoTieneTextoEnElResx_CuandoSeConsultaPorResourceManager()
    {
        CrearPlantillaTurnosCommandHandler.Mensajes.NombreDuplicado.Should().NotBeNullOrWhiteSpace();
    }
}
