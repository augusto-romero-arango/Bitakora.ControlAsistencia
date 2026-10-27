using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarSubFranjaFunction;

public class AgregarSubFranjaHorarioAbiertoTests : CommandHandlerAsyncTest<AgregarSubFranja>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<AgregarSubFranja> Handler =>
        new AgregarSubFranjaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task AgregarSubFranja_LanzaReglaDeNegocioDeclinadaException_CuandoAgregaDescansoAUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(4, 0), TipoSubFranja.Descanso, new TimeOnly(6, 0), new TimeOnly(6, 30)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Single().Descansos.Count, 0);
    }

    [Fact]
    public async Task AgregarSubFranja_LanzaReglaDeNegocioDeclinadaException_CuandoAgregaExtraAUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(new AgregarSubFranja(
            TurnoId, new TimeOnly(4, 0), TipoSubFranja.Extra, new TimeOnly(8, 0), new TimeOnly(9, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AgregarSubFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(),
            c => c.ObtenerDetalle().FranjasOrdinarias.Single().Extras.Count, 0);
    }
}
