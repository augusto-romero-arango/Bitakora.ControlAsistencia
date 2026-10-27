using AwesomeAssertions;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction;
using Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;
using Bitakora.ControlAsistencia.Programacion.Tests.CrearTurnoDeHorarioAbiertoFunction;
using Cosmos.EventSourcing.Abstractions.Commands;
using Cosmos.EventSourcing.Testing.Utilities;

namespace Bitakora.ControlAsistencia.Programacion.Tests.ModificarVentanaDeEntradaFunction;

public class ModificarVentanaDeEntradaHorarioAbiertoTests : CommandHandlerAsyncTest<ModificarVentanaDeEntrada>
{
    private static readonly Guid TurnoId = TurnoDeHorarioAbiertoEventos.TurnoId;

    protected override ICommandHandlerAsync<ModificarVentanaDeEntrada> Handler =>
        new ModificarVentanaDeEntradaCommandHandler(EventStore, PrivateEventSender);

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinadaException_CuandoLaAmplitudDifiereEnUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        var act = async () => await WhenAsync(new ModificarVentanaDeEntrada(TurnoId, new(3, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoDeHorarioAbierto}*");
        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, bool>(TurnoId.ToString(),
            c => c.Turno.TieneVentanaDeEntrada(HorasYMinutos.Crear(14, 0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_NoEmiteEventos_CuandoRepiteLaAmplitudVigenteDeUnTurnoDeHorarioAbierto()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana());

        await WhenAsync(new ModificarVentanaDeEntrada(TurnoId, new(14, 0)));

        Then(TurnoId.ToString());
        ThenIsPublishedPrivately();
        And<CatalogoTurnos, bool>(TurnoId.ToString(),
            c => c.Turno.TieneVentanaDeEntrada(HorasYMinutos.Crear(14, 0)), true);
    }

    [Fact]
    public async Task ModificarVentanaDeEntrada_LanzaReglaDeNegocioDeclinadaException_CuandoElTurnoAbiertoFueRetirado()
    {
        Given(TurnoId.ToString(), TurnoDeHorarioAbiertoEventos.Creado(), TurnoDeHorarioAbiertoEventos.Ventana(),
            TurnoRetirado.Crear(TurnoId));

        var act = async () => await WhenAsync(new ModificarVentanaDeEntrada(TurnoId, new(3, 0)));

        await act.Should().ThrowExactlyAsync<ReglaDeNegocioDeclinadaException>()
            .WithMessage($"*{ModificarVentanaDeEntradaCommandHandler.Mensajes.TurnoRetirado}*");
        Then(TurnoId.ToString());
        And<CatalogoTurnos, ResultadoAsignabilidadTurno>(TurnoId.ToString(),
            c => c.EvaluarAsignabilidad(), ResultadoAsignabilidadTurno.Retirado);
    }
}
