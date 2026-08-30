using System.Text.Json.Serialization.Metadata;

namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la cancelacion del turno diario asignado a un ControlDiario.
/// Se persiste en el stream de ControlDiarioAggregateRoot y no cruza el bus.
/// Issue #499: simetrico de TurnoDiarioAsignado -- misma terna de identidad del colaborador, mismo
/// patron de ctor privado + ConfigurarSerializacion para STJ/Marten (MEF-ADR-0012).
/// </summary>
public sealed class TurnoDiarioCancelado
{
    public string Id { get; private set; } = null!;
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public DateOnly Fecha { get; private set; }
    public Guid SolicitudCancelacionId { get; private set; }

    public TurnoDiarioCancelado(
        string id,
        ColaboradorProgramado colaborador,
        DateOnly fecha,
        Guid solicitudCancelacionId)
    {
        Id = id;
        Colaborador = colaborador;
        Fecha = fecha;
        SolicitudCancelacionId = solicitudCancelacionId;
    }

    // Constructor para Marten/serializacion
    private TurnoDiarioCancelado() { }

    // Stub de compilacion: la implementacion real (idéntica en forma a
    // TurnoDiarioAsignado.ConfigurarSerializacion) la escribe el implementer.
    public static void ConfigurarSerializacion(DefaultJsonTypeInfoResolver resolver) =>
        throw new NotImplementedException();
}
