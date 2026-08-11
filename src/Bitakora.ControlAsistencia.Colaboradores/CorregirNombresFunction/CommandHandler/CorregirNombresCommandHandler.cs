using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction.CommandHandler;

// Issue #351: handler del comando CorregirNombres. El mas simple del ciclo de vida --
// sin caso 409: CA-ADR-0030 solo exige el 404 de orquestacion (colaborador inexistente), sin
// eventos de fallo. El aggregate decide en silencio si hay algo que corregir (idempotencia por
// igualdad de valor, decision de refinamiento 2026-08-11).
// Flujo esperado (precedente TerminarVinculacionCommandHandler/ReingresarColaboradorCommandHandler):
//   1. Parseo tipado unico del borde: TipoIdentificacion.Desde(...) + Identificacion.Crear(...)
//      (normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los handlers hermanos).
//   2. NombreColaborador.Crear(...) con los 4 campos del comando.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si es
//      null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.CorregirNombres(nombre) -- nunca lanza; si el nombre es distinto por valor ya
//      quedo en _uncommittedEvents, el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #351 "Consumidores: ninguno").
// STUB (fase roja, issue #351): el cuerpo completo queda para el implementer.
public partial class CorregirNombresCommandHandler : ICommandHandlerAsync<CorregirNombres>
{
    private readonly IEventStore _eventStore;

    public CorregirNombresCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(CorregirNombres command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
