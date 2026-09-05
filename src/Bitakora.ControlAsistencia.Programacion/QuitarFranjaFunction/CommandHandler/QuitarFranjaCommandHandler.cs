using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarFranjaFunction.CommandHandler;

// Espejo de RetirarTurnoCommandHandler/AgregarFranjaCommandHandler en el canal de errores: sin
// invariantes de VO involucradas (este comando no construye nada), asi que no hay canal de
// ArgumentException que mezclar (CA-ADR-0030).
public partial class QuitarFranjaCommandHandler : ICommandHandlerAsync<QuitarFranja>
{
    private readonly IEventStore _eventStore;

    public QuitarFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(QuitarFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        // El arm final vuelve ruidoso un miembro nuevo del enum: sin el, un rechazo sin mensaje
        // mapeado saldria 202 como si la franja se hubiera quitado.
        var mensajeDeRechazo = catalogo.QuitarFranja(command.Franja) switch
        {
            ResultadoQuitarFranja.Quitada => null,
            ResultadoQuitarFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoQuitarFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            var otro => throw new NotSupportedException($"Resultado de QuitarFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new InvalidOperationException(mensajeDeRechazo);
    }
}
