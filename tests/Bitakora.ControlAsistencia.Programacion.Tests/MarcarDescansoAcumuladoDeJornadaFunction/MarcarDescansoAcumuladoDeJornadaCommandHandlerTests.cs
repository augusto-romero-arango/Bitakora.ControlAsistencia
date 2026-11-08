using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.MarcarDescansoAcumuladoDeJornadaFunction;

public class MarcarDescansoAcumuladoDeJornadaCommandHandlerTests : CommandHandlerAsyncTest<MarcarDescansoAcumuladoDeJornada>
{
    protected override ICommandHandlerAsync<MarcarDescansoAcumuladoDeJornada> Handler =>
        new MarcarDescansoAcumuladoDeJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static readonly Guid JornadaB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a62");

    private readonly FakeAseguradorJornadaPredeterminada _asegurador = new(Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60"));
    private FakeLectorLimitesJornada _lector = new();

    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(0, 0), 1);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id, Limites());

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_EmiteDescansoAcumuladoDeJornadaMarcado_CuandoLaJornadaNoEstaMarcada()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        And<Jornada, bool>(j => j.DescansoAcumulado, true);
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_FuncionaConModalidad_CuandoLaJornadaLaTiene()
    {
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Flexible);
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_NoEmiteEventos_CuandoLaJornadaYaEstaMarcada()
    {
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, true);
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_NoEmiteEventosNiLanza_CuandoYaEstaEnElEstadoAunqueOtraJornadaColisione()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, false),
            new JornadaDelCatalogo(JornadaB, Limites(), null, false));
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, true);
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_LanzaRecursoNoEncontradoException_CuandoJornadaNoExiste()
    {
        var act = async () => await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{MarcarDescansoAcumuladoDeJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaTieneLaIdentidadResultante()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, false),
            new JornadaDelCatalogo(JornadaB, Limites(), null, true));
        Given(Creada(GuidAggregateId));
        var act = async () => await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, false);
    }

    [Fact]
    public async Task MarcarDescansoAcumuladoDeJornada_Emite_CuandoOtraJornadaConLosMismosLimitesTieneOtraMarca()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, false),
            new JornadaDelCatalogo(JornadaB, Limites(), null, false));
        Given(Creada(GuidAggregateId));
        await WhenAsync(new MarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        And<Jornada, bool>(j => j.DescansoAcumulado, true);
    }
}
