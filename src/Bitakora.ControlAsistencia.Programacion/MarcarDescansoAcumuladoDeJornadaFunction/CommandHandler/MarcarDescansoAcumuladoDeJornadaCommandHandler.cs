using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.MarcarDescansoAcumuladoDeJornada;

namespace Bitakora.ControlAsistencia.Programacion.MarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;

public partial class MarcarDescansoAcumuladoDeJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
