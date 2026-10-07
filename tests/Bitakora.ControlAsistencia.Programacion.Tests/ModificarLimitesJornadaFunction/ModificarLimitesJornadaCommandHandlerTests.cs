using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaCommandHandlerTests : CommandHandlerAsyncTest<ModificarLimitesJornada>
{
    protected override ICommandHandlerAsync<ModificarLimitesJornada> Handler =>
        new ModificarLimitesJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static readonly Guid JornadaB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a62");

    private readonly FakeAseguradorJornadaPredeterminada _asegurador = new(Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60"));
    private FakeLectorLimitesJornada _lector = new();

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static ModificarLimitesJornada Comando(Guid id, int horasSemanales = 44) =>
        new(id, new(horasSemanales, 0), new(8, 0), new(0, 0), 1);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1));

    [Fact]
    public async Task ModificarLimitesJornada_EmiteLimitesJornadaModificados_CuandoLosLimitesCambian()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId));
        Then(LimitesJornadaModificados.Crear(GuidAggregateId, LimitesJornada.Crear(
            HorasYMinutos.Crear(44, 0), HorasYMinutos.Crear(8, 0), HorasYMinutos.Crear(0, 0), 1)));
        And<Jornada, int>(j => j.Describir().HorasSemanales.Horas, 44);
    }

    [Fact]
    public async Task ModificarLimitesJornada_NoEmiteEventos_CuandoLosLimitesYaSonLosVigentes()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, horasSemanales: 42));
        Then();
        And<Jornada, int>(j => j.Describir().HorasSemanales.Horas, 42);
    }

    [Fact]
    public async Task ModificarLimitesJornada_LanzaRecursoNoEncontradoException_CuandoJornadaNoExiste()
    {
        var act = async () => await WhenAsync(Comando(GuidAggregateId));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{ModificarLimitesJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task ModificarLimitesJornada_LanzaAggregateExceptionConTodosLosMensajes_CuandoLosLimitesViolanInvariantes()
    {
        Given(Creada(GuidAggregateId));
        var comando = Comando(GuidAggregateId) with { TopeDiario = new(0, 0), MinimoDiario = new(9, 0) };
        var act = async () => await WhenAsync(comando);
        var errores = (await act.Should().ThrowExactlyAsync<AggregateException>()).Which.InnerExceptions;
        errores.Select(e => e.Message).Should().Contain(LimitesJornada.Mensajes.MinimoMayorQueTope);
        errores.Should().HaveCountGreaterThan(1);
        Then();
        And<Jornada, int>(j => j.Describir().HorasSemanales.Horas, 42);
    }

    [Fact]
    public async Task ModificarLimitesJornada_LanzaReglaDeNegocioDeclinada_CuandoLosLimitesIgualanALosDeOtraJornada()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)), new JornadaDelCatalogo(JornadaB, Limites(40, 8)));
        Given(JornadaCreada.Crear(GuidAggregateId, Limites(42, 9)));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, horasSemanales: 40));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        ex.Message.Should().Contain("40 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then();
        And<Jornada, int>(j => j.Describir().HorasSemanales.Horas, 42);
    }

    [Fact]
    public async Task ModificarLimitesJornada_NoEmiteEventosNiLanza_CuandoLosLimitesSonLosPropios()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)), new JornadaDelCatalogo(JornadaB, Limites(40, 8)));
        Given(JornadaCreada.Crear(GuidAggregateId, Limites(42, 9)));
        await WhenAsync(new ModificarLimitesJornada(GuidAggregateId, new(42, 0), new(9, 0), new(0, 0), 1));
        Then();
        And<Jornada, int>(j => j.Describir().HorasSemanales.Horas, 42);
    }
}
