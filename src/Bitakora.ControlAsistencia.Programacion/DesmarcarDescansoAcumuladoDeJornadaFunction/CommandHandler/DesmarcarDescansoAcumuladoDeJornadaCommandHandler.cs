using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventSourcing.Abstractions.Commands;
using Comando = Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction.DesmarcarDescansoAcumuladoDeJornada;

namespace Bitakora.ControlAsistencia.Programacion.DesmarcarDescansoAcumuladoDeJornadaFunction.CommandHandler;

public partial class DesmarcarDescansoAcumuladoDeJornadaCommandHandler(
    IEventStore eventStore, IAseguradorJornadaPredeterminada asegurador, ILectorLimitesJornada lector)
    : ICommandHandlerAsync<Comando>
{
    public Task HandleAsync(Comando command, CancellationToken ct = default)
        => throw new NotImplementedException();
}
