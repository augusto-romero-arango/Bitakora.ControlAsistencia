using Bitakora.ControlAsistencia.Colaboradores.DomainEvents;
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

    public async Task HandleAsync(ComandoRegistrarColaborador command, CancellationToken ct = default)
    {
        // Borde HTTP: normalizar trim+MAYUSCULAS ANTES de Desde -- el VO de #348 es case-sensitive
        // por diseno para proteger la rehidratacion desde datos ya persistidos; la normalizacion de
        // ENTRADA es responsabilidad de este handler (MEF-ADR-0037 seccion 2, parseo tipado unico).
        var tipo = TipoIdentificacion.Desde(command.TipoIdentificacion.Trim().ToUpperInvariant());
        var identificacion = Identificacion.Crear(tipo, command.NumeroIdentificacion);

        var streamId = ColaboradorAggregateRoot.ComputarStreamId(identificacion);
        var existe = await _eventStore.ExistsAsync<ColaboradorAggregateRoot>(streamId, ct);
        if (existe)
            throw new InvalidOperationException(Mensajes.ColaboradorYaRegistrado);

        var nombre = NombreColaborador.Crear(
            command.PrimerNombre, command.SegundoNombre, command.PrimerApellido, command.SegundoApellido);

        var colaborador = ColaboradorAggregateRoot.Registrar(
            identificacion, nombre, command.CodigoColaborador, command.FechaInicio);

        _eventStore.StartStream(colaborador);
    }
}
