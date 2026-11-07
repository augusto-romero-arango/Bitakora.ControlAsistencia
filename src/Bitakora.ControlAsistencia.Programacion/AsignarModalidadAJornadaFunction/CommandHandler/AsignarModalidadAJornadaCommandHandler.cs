using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.AsignarModalidadAJornada;

namespace Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler;

public partial class AsignarModalidadAJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var modalidad = command.ToModalidadJornada();
        var predeterminadaId = await asegurador.AsegurarAsync(ct);
        var catalogo = await lector.ObtenerLimitesAsync(predeterminadaId, ct);
        var colision = jornada.BuscarColision(modalidad, catalogo);
        if (colision is not null)
            throw new ReglaDeNegocioDeclinadaException(string.Format(
                Mensajes.ModalidadDuplicada, DescripcionJornada.Componer(jornada.Limites, modalidad), colision.JornadaId));

        jornada.AsignarModalidad(modalidad);
    }
}
