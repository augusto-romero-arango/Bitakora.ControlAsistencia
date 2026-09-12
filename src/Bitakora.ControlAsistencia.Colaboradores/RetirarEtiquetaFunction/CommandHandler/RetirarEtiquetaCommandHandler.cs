using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

// SinCambios NO se traduce: es el no-op exitoso de MEF-ADR-0004 ("Estado ya alcanzado") -- el
// aggregate ya retorno sin agregar eventos y el handler debe terminar sin lanzar.
// VinculacionTerminada es la unica razon de rechazo real (CA-ADR-0030) -> InvalidOperationException
// /409 con mensaje .resx. El 404 es precondicion de orquestacion (MEF-ADR-0004 capa 2), no regla
// del aggregate. En el camino de exito el aggregate ya dejo EtiquetaRetirada en
// _uncommittedEvents -- el middleware persiste via SaveChanges. Sin publicacion a bus
// (event-sourcing puro: este evento no tiene consumidores).
public partial class RetirarEtiquetaCommandHandler : ICommandHandlerAsync<RetirarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public RetirarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RetirarEtiqueta command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // CorregirFechaInicioVinculacionCommandHandler/IniciarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion);
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        // Tell-don't-Ask: la normalizacion de una categoria aislada (sin Valor) vive en el VO
        // Etiqueta (#355), nunca en este handler.
        var categoriaNormalizada = Etiqueta.NormalizarCategoria(command.Categoria);

        var resultado = colaborador.RetirarEtiqueta(categoriaNormalizada);
        if (resultado == ResultadoRetiroEtiqueta.VinculacionTerminada)
            throw new InvalidOperationException(Mensajes.VinculacionTerminada);
    }
}
