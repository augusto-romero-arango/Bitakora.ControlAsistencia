using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;

public class CrearJornadaCommandHandlerTests : CommandHandlerAsyncTest<CrearJornada>
{
    protected override ICommandHandlerAsync<CrearJornada> Handler => new CrearJornadaCommandHandler(EventStore);

    private static CrearJornada Comando(Guid id) => new(id, new(42, 0), new(8, 0), new(0, 0), 1);

    private static JornadaCreada Evento(Guid id) => JornadaCreada.Crear(id,
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(8, 0),
            HorasYMinutos.Crear(0, 0), 1));

    [Fact]
    public async Task CrearJornada_EmiteJornadaCreada_CuandoIdEsNuevo()
    {
        Given();
        await WhenAsync(Comando(GuidAggregateId));
        Then(Evento(GuidAggregateId));
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_EmiteJornadaCreada_CuandoOtraJornadaTieneLosMismosLimites()
    {
        var otroId = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c");
        Given(otroId.ToString(), Evento(otroId));
        await WhenAsync(Comando(GuidAggregateId));
        Then(Evento(GuidAggregateId));
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_LanzaRecursoYaExisteException_CuandoStreamYaExiste()
    {
        Given(Evento(GuidAggregateId));
        var act = async () => await WhenAsync(Comando(GuidAggregateId));
        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearJornadaCommandHandler.Mensajes.JornadaYaExiste}*");
        Then();
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_LanzaRecursoYaExisteException_CuandoIdGeneralEstaReservado()
    {
        var otraId = GuidAggregateId;
        Given(Evento(otraId));
        var act = async () => await WhenAsync(Comando(JornadaGeneral.Id));
        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearJornadaCommandHandler.Mensajes.JornadaGeneralReservada}*");
        Then(JornadaGeneral.Id.ToString());
        And<Jornada, string>(j => j.Id, otraId.ToString());
    }

    [Fact]
    public async Task CrearJornada_PriorizaReservaGeneral_CuandoLimitesSonInvalidos()
    {
        Given(Evento(GuidAggregateId));
        var comando = Comando(JornadaGeneral.Id) with { TopeDiario = new(0, 60) };
        var act = async () => await WhenAsync(comando);
        await act.Should().ThrowExactlyAsync<RecursoYaExisteException>()
            .WithMessage($"*{CrearJornadaCommandHandler.Mensajes.JornadaGeneralReservada}*");
        Then(JornadaGeneral.Id.ToString());
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_AcumulaErroresDeLosTresCampos_CuandoHayHorasYMinutosInvalidos()
    {
        Given(Evento(GuidAggregateId));
        var comando = Comando(Guid.NewGuid()) with
        {
            HorasSemanales = new(-1, 60), TopeDiario = new(-2, 61), MinimoDiario = new(-3, -1)
        };
        var act = async () => await WhenAsync(comando);
        var errores = (await act.Should().ThrowExactlyAsync<AggregateException>()).Which.InnerExceptions;
        errores.Select(e => e.Message).Should().Contain(m => m == HorasYMinutos.Mensajes.HorasNegativas);
        errores.Select(e => e.Message).Should().Contain(m => m == HorasYMinutos.Mensajes.MinutosFueraDeRango);
        errores.Should().HaveCount(6);
        Then(comando.JornadaId.ToString());
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }
}
