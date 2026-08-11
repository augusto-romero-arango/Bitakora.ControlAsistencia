using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
using Bitakora.ControlAsistencia.Colaboradores.Entities;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction.CommandHandler;

// Issue #350: handler del comando ReingresarColaborador (precedente TerminarVinculacionCommandHandler).
// El aggregate declina con resultado (CA-ADR-0030): nunca lanza ni emite un evento de fallo
// persistido, y este handler traduce la razon del rechazo a la excepcion que el borde convierte en
// status code (MEF-ADR-0004 capa 2): stream inexistente -> 404, regla de negocio violada -> 409.
// En el camino de exito el aggregate ya dejo VinculacionIniciada en _uncommittedEvents -- el
// middleware persiste via SaveChanges. Sin publicacion a bus (event-sourcing puro, "Consumidores:
// ninguno").
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
