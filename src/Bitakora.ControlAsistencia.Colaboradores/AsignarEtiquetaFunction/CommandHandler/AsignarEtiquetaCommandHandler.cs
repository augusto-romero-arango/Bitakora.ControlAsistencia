using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction.CommandHandler;

// Issue #355: handler del comando AsignarEtiqueta.
// Flujo esperado (precedente CorregirFechaInicioVinculacionCommandHandler #352, MEF-ADR-0004 capa
// 2 -- CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los handlers hermanos).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. Etiqueta.Crear(command.Categoria, command.Valor) -- valida y normaliza el par (#353).
//   5. colaborador.AsignarEtiqueta(etiqueta) -- el aggregate declina con resultado o en silencio
//      (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoAsignacionEtiqueta.VinculacionTerminada -> throw
//          InvalidOperationException(Mensajes.VinculacionTerminada) (-> 409).
//        - ResultadoAsignacionEtiqueta.SinCambios -> idempotencia silenciosa, sin evento (-> 202).
//        - ResultadoAsignacionEtiqueta.Exitosa -> el aggregate ya agrego EtiquetaAsignada a
//          _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #355 "Consumidores: ninguno").
// STUB (fase roja, issue #355): el cuerpo completo queda para el implementer.
public partial class AsignarEtiquetaCommandHandler : ICommandHandlerAsync<AsignarEtiqueta>
{
    private readonly IEventStore _eventStore;

    public AsignarEtiquetaCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(AsignarEtiqueta command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // CorregirFechaInicioVinculacionCommandHandler/ReingresarColaboradorCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var etiqueta = Etiqueta.Crear(command.Categoria, command.Valor);

        var resultado = colaborador.AsignarEtiqueta(etiqueta);
        if (resultado == ResultadoAsignacionEtiqueta.VinculacionTerminada)
            throw new InvalidOperationException(Mensajes.VinculacionTerminada);
    }
}
