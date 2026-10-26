using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.AgregarFranjaFunction;

public class AgregarFranjaHorarioAbiertoTests : CommandHandlerAsyncTest<AgregarFranja>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<AgregarFranja> Handler =>
        new AgregarFranjaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task AgregarFranja_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoEsDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, int>(TurnoId.ToString(), c => c.ObtenerDetalle().FranjasOrdinarias.Count, 1);
    }

    [Fact]
    public async Task AgregarFranja_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoAbiertoFueRetirado()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(
            new AgregarFranja(TurnoId, new TimeOnly(14, 0), new TimeOnly(22, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{AgregarFranjaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }
}
