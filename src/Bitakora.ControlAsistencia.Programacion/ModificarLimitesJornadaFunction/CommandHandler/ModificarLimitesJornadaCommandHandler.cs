using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.ModificarLimitesJornada;

namespace Bitakora.ControlAsistencia.Programacion.ModificarLimitesJornadaFunction.CommandHandler;

public partial class ModificarLimitesJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector,
    IPrivateEventSender privateEventSender) : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var limites = command.ToLimitesJornada();
        var predeterminadaId = await asegurador.AsegurarAsync(ct);
        var catalogo = await lector.ObtenerConLimitesAsync(predeterminadaId, limites, ct);
        var igual = jornada.BuscarColisionDeLimites(limites, catalogo);
        if (igual is not null)
            throw new ReglaDeNegocioDeclinadaException(string.Format(
                Mensajes.LimitesDuplicados, DescripcionJornada.Componer(limites, igual.Modalidad, igual.DescansoAcumulado),
                igual.JornadaId));

        if (jornada.ModificarLimites(limites) == ResultadoModificarLimites.Modificados)
            await privateEventSender.PublishAsync(jornada.ComoLimitesActualizados());
    }
}
