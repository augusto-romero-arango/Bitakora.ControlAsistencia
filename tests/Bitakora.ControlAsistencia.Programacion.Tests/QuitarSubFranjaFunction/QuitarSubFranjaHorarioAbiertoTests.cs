using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarSubFranjaFunction;

public class QuitarSubFranjaHorarioAbiertoTests : CommandHandlerAsyncTest<QuitarSubFranja>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<QuitarSubFranja> Handler =>
        new QuitarSubFranjaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task QuitarSubFranja_LanzaReglaDeNegocioDeclinadaException_CuandoQuitaDescansoDeUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(
            new QuitarSubFranja(TurnoId, new TimeOnly(4, 0), TipoSubFranja.Descanso, new TimeOnly(6, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, int>(TurnoId.ToString(), c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    [Fact]
    public async Task QuitarSubFranja_LanzaReglaDeNegocioDeclinadaException_CuandoQuitaExtraDeUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(
            new QuitarSubFranja(TurnoId, new TimeOnly(4, 0), TipoSubFranja.Extra, new TimeOnly(8, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarSubFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, int>(TurnoId.ToString(), c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }
}
