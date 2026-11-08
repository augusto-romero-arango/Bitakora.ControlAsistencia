using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.DesmarcarDescansoAcumuladoDeJornadaFunction;

public class DesmarcarDescansoAcumuladoDeJornadaCommandHandlerTests : CommandHandlerAsyncTest<DesmarcarDescansoAcumuladoDeJornada>
{
    protected override ICommandHandlerAsync<DesmarcarDescansoAcumuladoDeJornada> Handler =>
        new DesmarcarDescansoAcumuladoDeJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static readonly Guid JornadaB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a62");

    private readonly FakeAseguradorJornadaPredeterminada _asegurador = new(Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60"));
    private FakeLectorLimitesJornada _lector = new();

    private static LimitesJornada Limites() =>
        LimitesJornada.Crear(HorasYMinutos.Crear(42, 0), HorasYMinutos.Crear(9, 0), HorasYMinutos.Crear(0, 0), 1);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id, Limites());

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_EmiteDescansoAcumuladoDeJornadaDesmarcado_CuandoLaJornadaEstaMarcada()
    {
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaDesmarcado.Crear(GuidAggregateId));
        And<Jornada, bool>(j => j.DescansoAcumulado, false);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_FuncionaConModalidad_CuandoLaJornadaLaTiene()
    {
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible),
            DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaDesmarcado.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Flexible);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_NoEmiteEventos_CuandoLaJornadaNoEstaMarcada()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, false);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_NoEmiteEventosNiLanza_CuandoYaEstaEnElEstadoAunqueOtraJornadaColisione()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, true),
            new JornadaDelCatalogo(JornadaB, Limites(), null, true));
        Given(Creada(GuidAggregateId));
        await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, false);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_LanzaRecursoNoEncontradoException_CuandoJornadaNoExiste()
    {
        var act = async () => await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{DesmarcarDescansoAcumuladoDeJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaTieneLaIdentidadResultante()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, true),
            new JornadaDelCatalogo(JornadaB, Limites(), null, false));
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        var act = async () => await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then();
        And<Jornada, bool>(j => j.DescansoAcumulado, true);
    }

    [Fact]
    public async Task DesmarcarDescansoAcumuladoDeJornada_Emite_CuandoOtraJornadaConLosMismosLimitesTieneOtraMarca()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(), null, true),
            new JornadaDelCatalogo(JornadaB, Limites(), null, true));
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new DesmarcarDescansoAcumuladoDeJornada(GuidAggregateId));
        Then(DescansoAcumuladoDeJornadaDesmarcado.Crear(GuidAggregateId));
        And<Jornada, bool>(j => j.DescansoAcumulado, false);
    }
}
