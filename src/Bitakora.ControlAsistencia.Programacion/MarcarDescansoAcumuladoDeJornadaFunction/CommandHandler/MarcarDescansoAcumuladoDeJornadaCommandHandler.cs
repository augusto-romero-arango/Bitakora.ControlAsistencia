using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.MarcarDescansoAcumuladoDeJornada;

namespace Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;

public partial class MarcarDescansoAcumuladoDeJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var jornada = await eventStore.GetAggregateRootAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (jornada is null)
            throw new RecursoNoEncontradoException(Mensajes.JornadaNoEncontrada);

        if (jornada.DescansoAcumulado)
            return;

        var predeterminadaId = await asegurador.AsegurarAsync(ct);
        var catalogo = await lector.ObtenerConLimitesAsync(predeterminadaId, jornada.Limites, ct);
        var colision = jornada.BuscarColisionDeMarca(true, catalogo);
        if (colision is not null)
            throw new ReglaDeNegocioDeclinadaException(string.Format(
                Mensajes.JornadaIdenticaExistente,
                DescripcionJornada.Componer(jornada.Limites, jornada.Modalidad, true), colision.JornadaId));

        jornada.MarcarDescansoAcumulado();
    }
}
