using Bitakora.ControlAsistencia.PrivateEvents.Colaboradores;
using Bitakora.ControlAsistencia.PrivateEvents.Programacion;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.Infraestructura;
using Cosmos.EventDriven.Abstractions;
using Cosmos.EventSourcing.Abstractions.Commands;

namespace Bitakora.ControlAsistencia.Programacion.ProgramarAusenciaFunction.CommandHandler;

public partial class ProgramarAusenciaCommandHandler : ICommandHandlerAsync<ProgramarAusencia>
{
    private readonly IEventStore _eventStore;
    private readonly IPrivateEventSender _privateEventSender;

    public ProgramarAusenciaCommandHandler(IEventStore eventStore, IPrivateEventSender privateEventSender)
    {
        _eventStore = eventStore;
        _privateEventSender = privateEventSender;
    }

    public async Task HandleAsync(ProgramarAusencia command, CancellationToken ct = default)
    {
        var motivo = MotivoAusencia.Desde(command.Motivo);
        var colaborador = new ColaboradorProgramado(
            command.Identificacion, command.CodigoColaborador, command.NombreCompleto);

        var ausencias = await _eventStore.GetAggregateRootAsync<AusenciasColaborador>(
            AusenciasColaborador.ComputarStreamId(command.CodigoColaborador), ct);

        if (ausencias is null)
        {
            var evento = new AusenciaProgramada(
                command.Id, colaborador, command.FechaInicio, command.FechaFin, motivo);
            _eventStore.StartStream(AusenciasColaborador.Iniciar(evento));
        }
        else
        {
            switch (ausencias.ProgramarAusencia(
                        command.Id, colaborador, command.FechaInicio, command.FechaFin, motivo))
            {
                case ResultadoProgramarAusencia.IdDuplicado:
                    throw new RecursoYaExisteException(Mensajes.AusenciaYaExiste);
                case ResultadoProgramarAusencia.ChocaConAusencia choque:
                    throw new ReglaDeNegocioDeclinadaException(
                        Mensajes.ChoqueConAusencia(choque.FechasEnConflicto, choque.Motivo));
            }
        }

        // El evento de bus lleva tipos de PrivateEvents y el motivo como texto plano (MEF-ADR-0012).
        var resumen = new ResumenColaborador(
            command.Identificacion, command.CodigoColaborador, command.NombreCompleto);
        IPrivateEvent[] eventosPrivados = Enumerable
            .Range(0, command.FechaFin.DayNumber - command.FechaInicio.DayNumber + 1)
            .Select(offset => new AusenciaDiariaProgramada(
                command.Id, resumen, command.FechaInicio.AddDays(offset), motivo.Nombre))
            .ToArray();

        await _privateEventSender.PublishAsync(eventosPrivados);
    }
}
