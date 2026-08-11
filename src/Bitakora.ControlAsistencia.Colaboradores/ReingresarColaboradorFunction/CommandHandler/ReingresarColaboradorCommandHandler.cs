using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

// Issue #350: handler del comando ReingresarColaborador.
// Flujo esperado (precedente TerminarVinculacionCommandHandler, MEF-ADR-0004 capa 2 -- CA-ADR-0030):
//   1. Normalizar y parsear TipoIdentificacion -> TipoIdentificacion.Desde(...) (borde HTTP:
//      normalizar trim+MAYUSCULAS ANTES de Desde, mismo criterio que los demas handlers del
//      dominio).
//   2. Identificacion.Crear(tipo, command.NumeroIdentificacion) -- normaliza el numero.
//   3. ComputarStreamId(identificacion) -> GetAggregateRootAsync<ColaboradorAggregateRoot> -- si
//      es null, throw KeyNotFoundException(Mensajes.ColaboradorNoEncontrado) (-> 404).
//   4. colaborador.Reingresar(command.CodigoColaborador, command.FechaInicio) -- el aggregate
//      declina con resultado (nunca lanza, nunca emite evento de fallo persistido):
//        - ResultadoReingresoColaborador.VinculacionAbierta -> throw
//          InvalidOperationException(Mensajes.VinculacionAbierta) (-> 409).
//        - ResultadoReingresoColaborador.FechaSolapaVinculacionAnterior -> throw
//          InvalidOperationException(Mensajes.FechaSolapaVinculacionAnterior) (-> 409).
//        - ResultadoReingresoColaborador.Exitosa -> el aggregate ya agrego VinculacionIniciada a
//          _uncommittedEvents; WhenAsync/el middleware persiste via SaveChanges.
// Sin publicacion a bus (event-sourcing puro, issue #350 "Consumidores: ninguno").
// STUB (fase roja, issue #350): el cuerpo completo queda para el implementer.
public partial class ReingresarColaboradorCommandHandler : ICommandHandlerAsync<ReingresarColaborador>
{
    private readonly IEventStore _eventStore;

    public ReingresarColaboradorCommandHandler(IEventStore eventStore) =>
        _eventStore = eventStore;

    public async Task HandleAsync(ReingresarColaborador command, CancellationToken ct = default)
    {
        // Parseo tipado unico del borde (MEF-ADR-0037 seccion 2), mismo criterio que
        // TerminarVinculacionCommandHandler.
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var colaborador = await _eventStore.GetAggregateRootAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (colaborador is null)
            throw new KeyNotFoundException(Mensajes.ColaboradorNoEncontrado);

        var resultado = colaborador.Reingresar(command.CodigoColaborador, command.FechaInicio);
        switch (resultado)
        {
            case ResultadoReingresoColaborador.VinculacionAbierta:
                throw new InvalidOperationException(Mensajes.VinculacionAbierta);
            case ResultadoReingresoColaborador.FechaSolapaVinculacionAnterior:
                throw new InvalidOperationException(Mensajes.FechaSolapaVinculacionAnterior);
        }
    }
}
