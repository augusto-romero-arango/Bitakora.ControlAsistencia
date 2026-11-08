using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarModalidadDeJornadaFunction;

public class QuitarModalidadDeJornadaCommandHandlerTests : CommandHandlerAsyncTest<QuitarModalidadDeJornada>
{
    protected override ICommandHandlerAsync<QuitarModalidadDeJornada> Handler =>
        new QuitarModalidadDeJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static readonly Guid JornadaB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a62");

    private readonly FakeAseguradorJornadaPredeterminada _asegurador = new(Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60"));
    private FakeLectorLimitesJornada _lector = new();

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id, Limites(42, 9));

    [Fact]
    public async Task QuitarModalidadDeJornada_EmiteModalidadDeJornadaQuitada_CuandoLaJornadaTieneModalidad()
    {
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then(ModalidadDeJornadaQuitada.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_NoEmiteEventos_CuandoLaJornadaYaNoTieneModalidad()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_NoEmiteEventosNiLanza_CuandoYaNoTieneModalidadAunqueOtraJornadaColisione()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9)));
        Given(Creada(GuidAggregateId));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_LanzaRecursoNoEncontradoException_CuandoJornadaNoExiste()
    {
        var act = async () => await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{QuitarModalidadDeJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaTieneLosMismosLimitesSinModalidad()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9)));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        var act = async () => await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Flexible);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_EmiteLaQuita_CuandoOtraJornadaTieneLosMismosLimitesConModalidad()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then(ModalidadDeJornadaQuitada.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_EmiteLaQuita_CuandoOtraJornadaSinModalidadTieneOtrosLimites()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible),
            new JornadaDelCatalogo(JornadaB, Limites(40, 8)));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then(ModalidadDeJornadaQuitada.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_EmiteLaQuita_CuandoOtraJornadaSinModalidadTieneLosMismosLimitesPeroOtraMarca()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible, true),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9)));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible),
            DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        Then(ModalidadDeJornadaQuitada.Crear(GuidAggregateId));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task QuitarModalidadDeJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaSinModalidadTieneLosMismosLimitesYMarca()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible, true),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), null, true));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible),
            DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        var act = async () => await WhenAsync(new QuitarModalidadDeJornada(GuidAggregateId));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Flexible);
    }
}
