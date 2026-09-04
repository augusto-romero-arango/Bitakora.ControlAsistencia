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

        var resultado = catalogo.AgregarFranja(franja);
        switch (resultado)
        {
            case ResultadoAgregarFranja.TurnoRetirado:
                throw new InvalidOperationException(Mensajes.TurnoRetirado);
            case ResultadoAgregarFranja.TurnoEsDescanso:
                throw new InvalidOperationException(Mensajes.TurnoEsDescanso);
            case ResultadoAgregarFranja.SeSolapaConOtraFranja:
                throw new InvalidOperationException(Mensajes.FranjaSeSolapa);
        }
    }
}
