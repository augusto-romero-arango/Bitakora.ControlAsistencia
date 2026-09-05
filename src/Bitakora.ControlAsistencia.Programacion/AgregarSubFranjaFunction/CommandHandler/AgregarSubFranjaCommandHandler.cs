using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AgregarSubFranjaFunction.CommandHandler;

// Mismo criterio de canales que AgregarFranjaCommandHandler (#602, CA-ADR-0030): la
// ArgumentException de FranjaOrdinaria.ConDescanso/ConExtra sube sin capturarse -- solo el
// resultado del aggregate (TurnoRetirado/TurnoEsDescanso/FranjaNoExiste) se traduce aqui.
public partial class AgregarSubFranjaCommandHandler : ICommandHandlerAsync<AgregarSubFranja>
{
    private readonly IEventStore _eventStore;

    public AgregarSubFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(AgregarSubFranja command, CancellationToken ct = default)
    {
        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        var resultado = command.Tipo switch
        {
            TipoSubFranja.Descanso =>
                catalogo.AgregarDescanso(command.Franja, command.Inicio, command.Fin),
            TipoSubFranja.Extra =>
                catalogo.AgregarExtra(command.Franja, command.Inicio, command.Fin),
            var otro => throw new NotSupportedException($"Tipo de sub-franja no mapeado: {otro}")
        };

        var mensajeDeRechazo = resultado switch
        {
            ResultadoAgregarSubFranja.Agregada => null,
            ResultadoAgregarSubFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoAgregarSubFranja.TurnoEsDescanso => Mensajes.TurnoEsDescanso,
            ResultadoAgregarSubFranja.FranjaNoExiste => Mensajes.FranjaNoExiste,
            var otro => throw new NotSupportedException($"Resultado de AgregarSubFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new InvalidOperationException(mensajeDeRechazo);
    }
}
