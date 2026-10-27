using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.ModificarVentanaDeEntrada;

namespace Bitakora.ControlAsistencia.Programacion.ModificarVentanaDeEntradaFunction.CommandHandler;

public partial class ModificarVentanaDeEntradaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var amplitud = command.ToAmplitud();

        var catalogo = await eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId.ToString(), ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        var rechazo = catalogo.ModificarVentanaDeEntrada(amplitud) switch
        {
            ResultadoModificarVentanaDeEntrada.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoModificarVentanaDeEntrada.TurnoDeHorarioAbierto => Mensajes.TurnoDeHorarioAbierto,
            ResultadoModificarVentanaDeEntrada.TurnoSinFranjas => Mensajes.TurnoSinFranjas,
            ResultadoModificarVentanaDeEntrada.CierrePasaMedianoche => Mensajes.CierrePasaMedianoche,
            _ => null
        };

        if (rechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(rechazo);

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await privateEventSender.PublishAsync(diseno);
    }
}
