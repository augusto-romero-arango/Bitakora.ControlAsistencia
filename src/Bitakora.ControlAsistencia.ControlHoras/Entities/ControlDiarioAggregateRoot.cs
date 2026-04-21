using Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;
using Bitakora.ControlAsistencia.Contracts.Programacion.ValueObjects;
using Bitakora.ControlAsistencia.ControlHoras.AdicionarMarcacionCuandoMarcacionRegistrada.Eventos;
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

    // HU-106: lista de marcaciones adicionadas al control diario
    // CA-3: crece al adicionar una marcacion nueva
    // CA-4: idempotencia nivel 2 - duplicado por minuto normalizado se ignora
    public List<MarcacionNormalizada> Marcaciones { get; private set; } = [];

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

    // HU-106: Apply que agrega la marcacion a la lista
    // Apply solo proyecta estado; la deteccion de duplicado vive en AdicionarMarcacion (CA-4).
    // Cuando el aggregate nace desde Iniciar(MarcacionAdicionada), este Apply asigna el Id del stream.
    // public: requerido para que TestStore.ApplyEvent lo encuentre via GetMethods()
    public void Apply(MarcacionAdicionada e)
    {
        Id = e.Id;
        Marcaciones.Add(new MarcacionNormalizada(e.TimestampNormalizado, e.TipoMarcacion));
    }

    // HU-106: segundo camino de creacion del ControlDiario, sin turno asignado
    // CA-5: si no existe ControlDiario para la fecha, se crea con este factory
    // CA-6: InformacionEmpleado y DetalleTurno quedan null
    internal static ControlDiarioAggregateRoot Iniciar(MarcacionAdicionada evento)
    {
        var control = new ControlDiarioAggregateRoot();
        control._uncommittedEvents.Add(evento);
        control.Apply(evento);
        return control;
    }

    // HU-106: agrega una marcacion al aggregate existente
    // CA-4: idempotencia nivel 2 - si ya existe una marcacion con el mismo minuto
    //        normalizado, se ignora silenciosamente sin emitir evento ni excepcion
    internal void AdicionarMarcacion(MarcacionAdicionada evento)
    {
        var yaExiste = Marcaciones.Any(m => m.TimestampNormalizado == evento.TimestampNormalizado);
        if (yaExiste) return;

        _uncommittedEvents.Add(evento);
        Apply(evento);
    }
}
