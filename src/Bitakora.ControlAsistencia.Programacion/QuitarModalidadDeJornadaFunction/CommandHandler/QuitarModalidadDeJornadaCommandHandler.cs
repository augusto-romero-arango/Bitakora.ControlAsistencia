using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.QuitarModalidadDeJornada;

namespace Bitakora.ControlAsistencia.Programacion.QuitarModalidadDeJornadaFunction.CommandHandler;

public partial class QuitarModalidadDeJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default) => throw new NotImplementedException();
}
