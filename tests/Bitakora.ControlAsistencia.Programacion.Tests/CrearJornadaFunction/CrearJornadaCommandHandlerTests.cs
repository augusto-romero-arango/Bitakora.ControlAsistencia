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
    private static readonly Guid Predeterminada = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60");
    private static readonly Guid JornadaX = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a61");

    private FakeAseguradorJornadaPredeterminada _asegurador = new(Predeterminada);
    private FakeLectorLimitesJornada _lector = new();

    protected override ICommandHandlerAsync<CrearJornada> Handler =>
        new CrearJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static LimitesJornada Limites(int semanales, int tope, int minimo, int descansos) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(minimo, 0), descansos);

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
    public async Task CrearJornada_EmiteJornadaCreada_CuandoElIdEsElQueFueGeneralReservado()
    {
        var id = Guid.Parse("00000000-0000-4000-8000-000000000001");
        Given();
        await WhenAsync(Comando(id));
        Then(id.ToString(), Evento(id));
        And<Jornada, string>(id.ToString(), j => j.Id, id.ToString());
    }

    [Fact]
    public async Task CrearJornada_AcumulaErroresDeLosTresCampos_CuandoHayHorasYMinutosInvalidos()
    {
        Given(Evento(GuidAggregateId));
        var comando = Comando(Guid.NewGuid()) with
        {
            HorasSemanales = new(-1, 60),
            TopeDiario = new(-2, 61),
            MinimoDiario = new(-3, -1)
        };
        var act = async () => await WhenAsync(comando);
        var errores = (await act.Should().ThrowExactlyAsync<AggregateException>()).Which.InnerExceptions;
        errores.Select(e => e.Message).Should().Contain(m => m == HorasYMinutos.Mensajes.HorasNegativas);
        errores.Select(e => e.Message).Should().Contain(m => m == HorasYMinutos.Mensajes.MinutosFueraDeRango);
        errores.Should().HaveCount(6);
        Then(comando.JornadaId.ToString());
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_LanzaReglaDeNegocioDeclinada_CuandoLosLimitesIgualanALosDeOtraJornada()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador, new JornadaDelCatalogo(JornadaX, Limites(40, 8, 0, 1)));
        Given(JornadaX.ToString(), Evento(JornadaX));
        var comando = Comando(GuidAggregateId) with { HorasSemanales = new(40, 0) };
        var act = async () => await WhenAsync(comando);
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaX.ToString());
        ex.Message.Should().Contain("40 h semanales, tope diario 8 h, sin mínimo diario, 1 día de descanso por semana");
        Then(GuidAggregateId.ToString());
        And<Jornada, string>(JornadaX.ToString(), j => j.Id, JornadaX.ToString());
    }

    [Fact]
    public async Task CrearJornada_EmiteJornadaCreada_CuandoLosLimitesDifierenEnDiasDeDescanso()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador, new JornadaDelCatalogo(JornadaX, Limites(40, 8, 0, 1)));
        Given();
        var comando = Comando(GuidAggregateId) with { HorasSemanales = new(40, 0), DiasDescansoPorSemana = 2 };
        await WhenAsync(comando);
        Then(JornadaCreada.Crear(GuidAggregateId, Limites(40, 8, 0, 2)));
        And<Jornada, string>(j => j.Id, GuidAggregateId.ToString());
    }

    [Fact]
    public async Task CrearJornada_LanzaConElIdDeLaPredeterminada_CuandoLosLimitesIgualanALosDeLaPredeterminadaAsegurada()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador, new JornadaDelCatalogo(Predeterminada, Limites(42, 8, 0, 1)));
        Given(Predeterminada.ToString(), Evento(Predeterminada));
        var act = async () => await WhenAsync(Comando(GuidAggregateId));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(Predeterminada.ToString());
        _asegurador.Invocaciones.Should().Be(1);
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then(GuidAggregateId.ToString());
        And<Jornada, string>(Predeterminada.ToString(), j => j.Id, Predeterminada.ToString());
    }
}
