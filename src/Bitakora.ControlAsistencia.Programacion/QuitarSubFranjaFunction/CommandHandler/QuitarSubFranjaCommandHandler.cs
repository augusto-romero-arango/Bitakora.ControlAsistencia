using Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.QuitarSubFranjaFunction.CommandHandler;

// Espejo de QuitarFranjaCommandHandler (#604): este comando tampoco construye ningun VO, asi que
// no hay canal de ArgumentException que mezclar con el de las reglas de negocio (CA-ADR-0030).
public partial class QuitarSubFranjaCommandHandler : ICommandHandlerAsync<QuitarSubFranja>
{
    private readonly IEventStore _eventStore;

    public QuitarSubFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(QuitarSubFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        var resultado = command.Tipo switch
        {
            TipoSubFranja.Descanso => catalogo.QuitarDescanso(command.Franja, command.Inicio),
            TipoSubFranja.Extra => catalogo.QuitarExtra(command.Franja, command.Inicio),
            var otro => throw new NotSupportedException($"Tipo de sub-franja no mapeado: {otro}")
        };

        var mensajeDeRechazo = resultado switch
        {
            ResultadoQuitarSubFranja.Quitada => null,
            ResultadoQuitarSubFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoQuitarSubFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            ResultadoQuitarSubFranja.SubFranjaNoExiste => Mensajes.SubFranjaNoExiste,
            var otro => throw new NotSupportedException($"Resultado de QuitarSubFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new InvalidOperationException(mensajeDeRechazo);
    }
}
