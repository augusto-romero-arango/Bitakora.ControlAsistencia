using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearJornadaFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AsignarModalidadAJornadaFunction;

public class AsignarModalidadAJornadaCommandHandlerTests : CommandHandlerAsyncTest<AsignarModalidadAJornada>
{
    protected override ICommandHandlerAsync<AsignarModalidadAJornada> Handler =>
        new AsignarModalidadAJornadaCommandHandler(EventStore, _asegurador, _lector);

    private static readonly Guid JornadaB = Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a62");

    private readonly FakeAseguradorJornadaPredeterminada _asegurador = new(Guid.Parse("0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a60"));
    private FakeLectorLimitesJornada _lector = new();

    private static LimitesJornada Limites(int semanales, int tope) =>
        LimitesJornada.Crear(HorasYMinutos.Crear(semanales, 0), HorasYMinutos.Crear(tope, 0),
            HorasYMinutos.Crear(0, 0), 1);

    private static JornadaCreada Creada(Guid id) => JornadaCreada.Crear(id, Limites(42, 9));

    private static AsignarModalidadAJornada Comando(Guid id, string modalidad) => new(id, modalidad);

    [Fact]
    public async Task AsignarModalidadAJornada_EmiteModalidadDeJornadaAsignada_CuandoLaJornadaNoTieneModalidad()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_ReemplazaLaModalidad_CuandoLaJornadaTeniaOtra()
    {
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_AceptaLaModalidadEnMinusculas_CuandoCoincideConElCatalogo()
    {
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, "turnossucesivos"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.TurnosSucesivos));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.TurnosSucesivos);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_NoEmiteEventos_CuandoLaJornadaYaTieneEsaModalidad()
    {
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_NoEmiteEventosNiLanza_CuandoYaTieneLaModalidadAunqueOtraJornadaColisione()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Ordinaria),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_LanzaRecursoNoEncontradoException_CuandoJornadaNoExiste()
    {
        var act = async () => await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarModalidadAJornadaCommandHandler.Mensajes.JornadaNoEncontrada}*");
        Then(GuidAggregateId.ToString());
    }

    [Fact]
    public async Task AsignarModalidadAJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaTieneLosMismosLimitesYModalidad()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria));
        Given(Creada(GuidAggregateId));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        _lector.AseguradorInvocadoAntesDeLeer.Should().BeTrue();
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_LanzaReglaDeNegocioDeclinada_CuandoLaOtraJornadaColisionanteEstaDesactivada()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), ModalidadJornada.Flexible),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria));
        Given(Creada(GuidAggregateId),
            ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Flexible));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString());
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Flexible);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_EmiteLaAsignacion_CuandoOtraJornadaTieneLosMismosLimitesYDistintaModalidad()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Flexible));
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_EmiteLaAsignacion_CuandoOtraJornadaTieneLaModalidadConOtrosLimites()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)),
            new JornadaDelCatalogo(JornadaB, Limites(40, 8), ModalidadJornada.Ordinaria));
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_EmiteLaAsignacion_CuandoOtraJornadaConLosMismosLimitesYModalidadEstaMarcadaYLaPropiaNo()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9)),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria, DescansoAcumulado: true));
        Given(Creada(GuidAggregateId));
        await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        Then(ModalidadDeJornadaAsignada.Crear(GuidAggregateId, ModalidadJornada.Ordinaria));
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, ModalidadJornada.Ordinaria);
    }

    [Fact]
    public async Task AsignarModalidadAJornada_LanzaReglaDeNegocioDeclinada_CuandoOtraJornadaConLosMismosLimitesModalidadYMarcaExiste()
    {
        _lector = new FakeLectorLimitesJornada(_asegurador,
            new JornadaDelCatalogo(GuidAggregateId, Limites(42, 9), null, true),
            new JornadaDelCatalogo(JornadaB, Limites(42, 9), ModalidadJornada.Ordinaria, DescansoAcumulado: true));
        Given(Creada(GuidAggregateId), DescansoAcumuladoDeJornadaMarcado.Crear(GuidAggregateId));
        var act = async () => await WhenAsync(Comando(GuidAggregateId, "Ordinaria"));
        var ex = (await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()).Which;
        ex.Message.Should().Contain(JornadaB.ToString())
            .And.Contain(ModalidadJornada.Mensajes.LabelDescansoAcumulado);
        Then();
        And<Jornada, ModalidadJornada?>(j => j.Modalidad, null);
    }
}
