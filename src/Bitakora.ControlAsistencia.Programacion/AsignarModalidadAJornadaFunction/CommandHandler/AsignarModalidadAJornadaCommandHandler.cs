using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.AsignarModalidadAJornada;

namespace Bitakora.ControlAsistencia.Programacion.AsignarModalidadAJornadaFunction.CommandHandler;

public partial class AsignarModalidadAJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default) => throw new NotImplementedException();
}
