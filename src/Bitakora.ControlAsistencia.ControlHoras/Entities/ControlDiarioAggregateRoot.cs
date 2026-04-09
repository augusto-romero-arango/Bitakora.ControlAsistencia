using Bitakora.ControlAsistencia.Contracts.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AsignarTurnoCuandoProgramacionTurnoDiarioSolicitadaFunction.Eventos;
using Cosmos.EventSourcing.Abstractions;

namespace Bitakora.ControlAsistencia.ControlHoras.Entities;

// HU-12: Aggregate root del dia de trabajo de un empleado
// Identidad: EmpleadoId + Fecha como stream ID determinista (CA-7)
// ADR-0015: partial class para soportar clase Mensajes en archivo separado si se requiere
public partial class ControlDiarioAggregateRoot : AggregateRoot
{
    // CA-6: estado que actualiza al aplicar TurnoDiarioAsignado
    public InformacionEmpleado? InformacionEmpleado { get; private set; }
    public DateOnly Fecha { get; private set; }
    public DetalleTurno? DetalleTurno { get; private set; }

    // Trazabilidad: id de la ultima solicitud que asigno un turno (CA-5)
    public Guid UltimaSolicitudId { get; private set; }

    // CA-7: stream ID determinista: "{EmpleadoId}:{Fecha:yyyy-MM-dd}"
    // CA-8: dos mensajes con mismo EmpleadoId+Fecha comparten el mismo stream
    public static string ComputarStreamId(string empleadoId, DateOnly fecha) =>
        $"{empleadoId}:{fecha:yyyy-MM-dd}";

    // CA-6: actualiza estado interno al aplicar el evento
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(TurnoDiarioAsignado e)
    {
        Id = e.Id;
        InformacionEmpleado = e.InformacionEmpleado;
        Fecha = e.Fecha;
        DetalleTurno = e.DetalleTurno;
        UltimaSolicitudId = e.SolicitudId;
    }

    // Factory: crea el aggregate con el evento en _uncommittedEvents para StartStream
    internal static ControlDiarioAggregateRoot Iniciar(TurnoDiarioAsignado evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // Agrega un nuevo turno al aggregate existente (caso CA-4)
    internal void AsignarTurno(TurnoDiarioAsignado evento)
    {
        _uncommittedEvents.Add(evento);
        Apply(evento);
    }
}
