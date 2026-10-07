using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarLimitesJornadaFunction;

public class ModificarLimitesJornadaCommandHandlerTests : CommandHandlerAsyncTest<ModificarLimitesJornada>
{
    protected override ICommandHandlerAsync<ModificarLimitesJornada> Handler =>
        new ModificarLimitesJornadaCommandHandler(EventStore);

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
        Then();
        And<Jornada, string?>(j => j.Id, null);
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
}
