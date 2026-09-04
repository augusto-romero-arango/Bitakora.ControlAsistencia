using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;

// El handler construye FranjaOrdinaria (invariantes del VO) ANTES de leer el aggregate: una
// ArgumentException del factory sube sin tocar el catalogo (CA-ADR-0030 -- dos canales de error
// distintos, nunca mezclados en el mismo metodo del aggregate).
public partial class AgregarFranjaCommandHandler : ICommandHandlerAsync<AgregarFranja>
{
    private readonly IEventStore _eventStore;

    public AgregarFranjaCommandHandler(IEventStore eventStore) => _eventStore = eventStore;

    public async Task HandleAsync(AgregarFranja command, CancellationToken ct = default)
    {
        var franja = FranjaOrdinaria.Crear(
            command.Inicio, command.Fin, command.DiaOffsetFin ?? 0, sede: command.Sede);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new KeyNotFoundException(Mensajes.TurnoNoEncontrado);

        // El arm final vuelve ruidoso un miembro nuevo del enum: sin el, un rechazo sin mensaje
        // mapeado saldria 202 como si la franja se hubiera agregado.
        var mensajeDeRechazo = catalogo.AgregarFranja(franja) switch
        {
            ResultadoAgregarFranja.Agregada => null,
            ResultadoAgregarFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoAgregarFranja.TurnoEsDescanso => Mensajes.TurnoEsDescanso,
            ResultadoAgregarFranja.SeSolapaConOtraFranja => Mensajes.FranjaSeSolapa,
            var otro => throw new NotSupportedException($"Resultado de AgregarFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new InvalidOperationException(mensajeDeRechazo);
    }
}
