using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;
using ComandoRegistrarColaborador = Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.RegistrarColaborador;

namespace Bitakora.ControlAsistencia.Colaboradores.RegistrarColaborador.CommandHandler;

// Issue #330: handler del comando RegistrarColaborador.
// Flujo esperado (precedente CrearTurnoCommandHandler, MEF-ADR-0004 capa 2):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde -- el VO de #348 es case-sensitive por diseno,
//      para proteger la rehidratacion desde datos ya persistidos; la normalizacion de ENTRADA es
//      responsabilidad de este handler, no del VO).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero (trim+MAYUS).
//   3. ComputarStreamId(identificacion) -> ExistsAsync<ColaboradorAggregateRoot> -- si existe,
//      throw InvalidOperationException(Mensajes.ColaboradorYaRegistrado) (-> 409 Conflict).
//   4. NombreColaborador.Crear(...) + ColaboradorAggregateRoot.Registrar(...) -> StartStream.
// Sin publicacion a bus (event-sourcing puro, issue #330 "Consumidores: ninguno").
// STUB (fase roja, issue #330): el cuerpo completo queda para el implementer.
public partial class RegistrarColaboradorCommandHandler : ICommandHandlerAsync<ComandoRegistrarColaborador>
{
    private readonly IEventStore _eventStore;

    public RegistrarColaboradorCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public Task HandleAsync(ComandoRegistrarColaborador command, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
