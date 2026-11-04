using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.DescartarBorradorDeProgramacionFunction.CommandHandler;

public partial class DescartarBorradorDeProgramacionCommandHandler : ICommandHandlerAsync<DescartarBorradorDeProgramacion>
{
    private readonly IEventStore _eventStore;

    public DescartarBorradorDeProgramacionCommandHandler(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task HandleAsync(DescartarBorradorDeProgramacion command, CancellationToken ct = default)
    {
        foreach (var mes in MesesDelRango(command.Desde, command.Hasta))
        {
            var borrador = await _eventStore.GetAggregateRootAsync<BorradorMensual>(
                BorradorMensual.ComputarStreamId(command.CodigoColaborador, mes), ct);
            if (borrador is null)
                continue;

            borrador.Descartar([.. borrador.FechasEnBorrador.Where(f => f >= command.Desde && f <= command.Hasta)]);
        }
    }

    private static IEnumerable<DateOnly> MesesDelRango(DateOnly desde, DateOnly hasta)
    {
        for (var mes = new DateOnly(desde.Year, desde.Month, 1); mes <= hasta; mes = mes.AddMonths(1))
            yield return mes;
    }
}
