using AwesomeAssertions;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction;
using Bitakora.ControlAsistencia.Colaboradores.AsignarJornadaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Bitakora.ControlAsistencia.Colaboradores.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Colaboradores.Tests.AsignarJornadaFunction;

public class AsignarJornadaCommandHandlerTests : CommandHandlerAsyncTest<AsignarJornada>
{
    private const string Numero = "79543210";
    private const string StreamId = "CC-79543210";
    private static readonly Guid JornadaUno = Guid.Parse("12345678-1234-4123-8123-123456789abc");
    private static readonly Guid JornadaDos = Guid.Parse("abcdef12-3456-4567-8123-abcdef123456");
    private static readonly Guid JornadaGeneral = Guid.Parse("00000000-0000-4000-8000-000000000001");

    protected override ICommandHandlerAsync<AsignarJornada> Handler => new AsignarJornadaCommandHandler(EventStore);

    private static AsignarJornada Comando(Guid jornadaId) => new("CC", Numero, jornadaId);

    private static ColaboradorRegistrado Registro() => new(
        Identificacion.Crear(TipoIdentificacion.CC, Numero),
        NombreColaborador.Crear("Luis", "Augusto", "Barreto", null));

    private static VinculacionIniciada Inicio() => new("COL-001", new DateOnly(2026, 1, 15));

    [Fact]
    public async Task AsignarJornada_EmiteJornadaAsignada_CuandoNoTieneJornada()
    {
        Given(StreamId, Registro(), Inicio());

        await WhenAsync(Comando(JornadaUno));

        Then(StreamId, new JornadaAsignada(JornadaUno));
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaUno);
        ThenIsPublishedPrivately();
        ThenIsPublishedPublicly();
    }

    [Fact]
    public async Task AsignarJornada_EmiteJornadaAsignada_CuandoReemplazaOtraJornada()
    {
        Given(StreamId, Registro(), Inicio(), new JornadaAsignada(JornadaUno));

        await WhenAsync(Comando(JornadaDos));

        Then(StreamId, new JornadaAsignada(JornadaDos));
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaDos);
    }

    [Fact]
    public async Task AsignarJornada_NoEmiteEventos_CuandoYaTieneLaJornadaSolicitada()
    {
        Given(StreamId, Registro(), Inicio(), new JornadaAsignada(JornadaDos));

        await WhenAsync(Comando(JornadaDos));

        Then(StreamId);
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaDos);
        ThenIsPublishedPrivately();
        ThenIsPublishedPublicly();
    }

    [Fact]
    public async Task AsignarJornada_EmiteJornadaAsignada_CuandoSeAsignaLaGeneral()
    {
        Given(StreamId, Registro(), Inicio());

        await WhenAsync(Comando(JornadaGeneral));

        Then(StreamId, new JornadaAsignada(JornadaGeneral));
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaGeneral);
    }

    [Fact]
    public async Task AsignarJornada_EmiteJornadaAsignada_CuandoHayReingresoConLaMismaJornadaAnterior()
    {
        Given(StreamId, Registro(), Inicio(), new JornadaAsignada(JornadaUno),
            new VinculacionTerminada(new DateOnly(2026, 6, 1)),
            new VinculacionIniciada("COL-002", new DateOnly(2026, 7, 1)));

        await WhenAsync(Comando(JornadaUno));

        Then(StreamId, new JornadaAsignada(JornadaUno));
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaUno);
    }

    [Fact]
    public async Task AsignarJornada_LanzaReglaDeNegocioDeclinadaException_CuandoLaVinculacionTermino()
    {
        Given(StreamId, Registro(), Inicio(), new VinculacionTerminada(new DateOnly(2026, 6, 1)));

        var act = async () => await WhenAsync(Comando(JornadaUno));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamId);
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, null);
    }

    [Fact]
    public async Task AsignarJornada_LanzaReglaDeNegocioDeclinadaException_CuandoHayPreavisoFuturo()
    {
        Given(StreamId, Registro(), Inicio(), new VinculacionTerminada(new DateOnly(2030, 12, 31)));

        var act = async () => await WhenAsync(Comando(JornadaUno));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamId);
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, null);
    }

    [Fact]
    public async Task AsignarJornada_LanzaReglaDeNegocioDeclinadaException_CuandoJornadaCoincidePeroVinculacionTermino()
    {
        Given(StreamId, Registro(), Inicio(), new JornadaAsignada(JornadaUno),
            new VinculacionTerminada(new DateOnly(2026, 6, 1)));

        var act = async () => await WhenAsync(Comando(JornadaUno));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AsignarJornadaCommandHandler.Mensajes.VinculacionTerminada}*");
        Then(StreamId);
        And<ColaboradorAggregateRoot, Guid?>(StreamId, c => c.JornadaId, JornadaUno);
    }

    [Fact]
    public async Task AsignarJornada_LanzaRecursoNoEncontradoException_CuandoColaboradorNoExiste()
    {
        var act = async () => await WhenAsync(Comando(JornadaUno));

        await act.Should().ThrowExactlyAsync<RecursoNoEncontradoException>()
            .WithMessage($"*{AsignarJornadaCommandHandler.Mensajes.ColaboradorNoEncontrado}*");
        Then(StreamId);
    }
}
