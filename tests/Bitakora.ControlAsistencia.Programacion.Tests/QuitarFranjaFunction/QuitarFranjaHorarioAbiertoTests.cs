using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.QuitarFranjaFunction;

public class QuitarFranjaHorarioAbiertoTests : CommandHandlerAsyncTest<QuitarFranja>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<QuitarFranja> Handler =>
        new QuitarFranjaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task QuitarFranja_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(4, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, int>(TurnoId.ToString(), c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    [Fact]
    public async Task QuitarFranja_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoAbiertoFueRetirado()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new QuitarFranja(TurnoId, new TimeOnly(4, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{QuitarFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }
}
