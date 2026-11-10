namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Se persiste en el stream de SolicitudCancelacionAggregateRoot; no se publica al Service Bus. Sin
/// turno ni sede: cancelar no transporta plan.
/// </summary>
public sealed class CancelacionProgramacionSolicitada
{
    public Guid Id { get; private set; }
    public ColaboradorProgramado Colaborador { get; private set; } = null!;
    public IReadOnlyList<DateOnly> Fechas { get; private set; } = [];

    public CancelacionProgramacionSolicitada(
        Guid id,
        ColaboradorProgramado colaborador,
        IReadOnlyList<DateOnly> fechas)
    {
        Id = id;
        Colaborador = colaborador;
        Fechas = fechas;
    }

    // Constructor para Marten/serializacion
    private CancelacionProgramacionSolicitada() { }
}
