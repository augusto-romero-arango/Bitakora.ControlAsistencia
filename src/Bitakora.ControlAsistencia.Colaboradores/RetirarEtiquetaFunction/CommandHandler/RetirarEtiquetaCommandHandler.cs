using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

// Issue #355: handler del comando RetirarEtiqueta. Mecanismo "declinar con resultado" puro
// (CA-ADR-0030, precedente CorregirFechaInicioVinculacionCommandHandler #352) -- sin variante
// silenciosa, a diferencia de AsignarEtiqueta: tanto CategoriaInexistente (CA-4, el typo debe
// aflorar) como VinculacionTerminada se traducen a InvalidOperationException/409 con mensaje .resx.
// El 404 es precondicion de orquestacion (MEF-ADR-0004 capa 2), no regla del aggregate. En el
// camino de exito el aggregate ya dejo EtiquetaRetirada en _uncommittedEvents -- el middleware
// persiste via SaveChanges. Sin publicacion a bus (event-sourcing puro, issue #355 "Consumidores:
// ninguno").
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
        switch (resultado)
        {
            case ResultadoRetiroEtiqueta.CategoriaInexistente:
                throw new InvalidOperationException(Mensajes.CategoriaInexistente);
            case ResultadoRetiroEtiqueta.VinculacionTerminada:
                throw new InvalidOperationException(Mensajes.VinculacionTerminada);
        }
    }
}
