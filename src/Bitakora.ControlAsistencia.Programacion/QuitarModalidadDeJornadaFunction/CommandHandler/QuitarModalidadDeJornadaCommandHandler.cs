using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.QuitarModalidadDeJornada;

namespace Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler;

public partial class QuitarModalidadDeJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        var predeterminadaId = await asegurador.AsegurarAsync(ct);
        var catalogo = await lector.ObtenerConLimitesAsync(predeterminadaId, jornada.Limites, ct);
        var colision = jornada.BuscarColision(null, catalogo);
        if (colision is not null)
            throw new ReglaDeNegocioDeclinadaException(string.Format(
                Mensajes.LimitesDuplicadosSinModalidad, jornada.Limites, colision.JornadaId));

        jornada.QuitarModalidad();
    }
}
