using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.AgregarFranjaFunction.CommandHandler;

// FranjaOrdinaria se construye ANTES de leer el aggregate: la ArgumentException del factory sube sin
// tocar el catalogo, y el aggregate solo declina con resultado (CA-ADR-0030: dos canales de error).
public partial class AgregarFranjaCommandHandler : ICommandHandlerAsync<AgregarFranja>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public AgregarFranjaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(AgregarFranja command, CancellationToken ct = default)
    {
        var franja = FranjaOrdinaria.Crear(
            command.Inicio, command.Fin, command.DiaOffsetFin ?? 0, sede: command.Sede);

        var catalogo = await _eventStore.GetAggregateRootAsync<CatalogoTurnos>(command.TurnoId, ct);
        if (catalogo is null)
            throw new RecursoNoEncontradoException(Mensajes.TurnoNoEncontrado);

        var mensajeDeRechazo = catalogo.AgregarFranja(franja) switch
        {
            ResultadoAgregarFranja.Agregada => null,
            ResultadoAgregarFranja.TurnoRetirado => Mensajes.TurnoRetirado,
            ResultadoAgregarFranja.TurnoDeHorarioAbierto => Mensajes.TurnoDeHorarioAbierto,
            ResultadoAgregarFranja.TurnoEsDescanso => Mensajes.TurnoEsDescanso,
            ResultadoAgregarFranja.SeSolapaConOtraFranja => Mensajes.FranjaSeSolapa,
            ResultadoAgregarFranja.CierrePasaMedianoche => Mensajes.CierrePasaMedianoche,
            var otro => throw new NotSupportedException($"Resultado de AgregarFranja no mapeado: {otro}")
        };

        if (mensajeDeRechazo is not null)
            throw new ReglaDeNegocioDeclinadaException(mensajeDeRechazo);

        var diseno = catalogo.ObtenerDisenoPublicable();
        if (diseno is not null)
            await _privateEventSender.PublishAsync(diseno);
    }
}
