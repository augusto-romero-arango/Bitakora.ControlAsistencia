using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CrearJornada;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction.CommandHandler;

public partial class CrearJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector) : ICommandHandlerAsync<Comando>
{
    public async Task HandleAsync(Comando command, CancellationToken ct = default)
    {
        var existe = await eventStore.ExistsAsync<Jornada>(command.JornadaId.ToString(), ct);
        if (existe)
            throw new RecursoYaExisteException(Mensajes.JornadaYaExiste);

        var limites = command.ToLimitesJornada();
        eventStore.StartStream(Jornada.Iniciar(JornadaCreada.Crear(command.JornadaId, limites)));
    }
}
