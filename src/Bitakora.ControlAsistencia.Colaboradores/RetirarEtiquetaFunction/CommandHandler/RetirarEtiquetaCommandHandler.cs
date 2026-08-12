using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.RetirarEtiquetaFunction.CommandHandler;

// Issue #355: handler del comando RetirarEtiqueta.
// Flujo esperado (precedente CorregirFechaInicioVinculacionCommandHandler #352, MEF-ADR-0004 capa
// 2 -- CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los handlers hermanos).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. Etiqueta.NormalizarCategoria(command.Categoria) -- Tell-don't-Ask: la normalizacion de una
//      categoria aislada (sin Valor) vive en el VO (#355, ver Etiqueta.cs), nunca en este handler.
//   5. colaborador.RetirarEtiqueta(categoriaNormalizada) -- el aggregate declina con resultado
//      (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoRetiroEtiqueta.CategoriaInexistente -> throw
//          InvalidOperationException(Mensajes.CategoriaInexistente) (-> 409).
//        - ResultadoRetiroEtiqueta.VinculacionTerminada -> throw
//          InvalidOperationException(Mensajes.VinculacionTerminada) (-> 409).
//        - ResultadoRetiroEtiqueta.Exitosa -> el aggregate ya agrego EtiquetaRetirada a
//          _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #355 "Consumidores: ninguno").
// STUB (fase roja, issue #355): el cuerpo completo queda para el implementer.
public partial class RetirarEtiquetaCommandHandler : ICommandHandlerAsync<RetirarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public RetirarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(RetirarEtiqueta command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // CorregirFechaInicioVinculacionCommandHandler/ReingresarColaboradorCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
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
