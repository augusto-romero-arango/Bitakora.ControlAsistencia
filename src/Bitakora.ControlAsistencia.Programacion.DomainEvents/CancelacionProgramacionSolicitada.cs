namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Evento de event sourcing (privado). Se persiste en el stream de SolicitudCancelacionAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
/// <remarks>
/// Colaborador tipa con el record propio de este ensamblado (ColaboradorProgramado), nunca con el de
/// PublicEvents ni PrivateEvents: tres islas sin referencias cruzadas (MEF-ADR-0039 decision 2).
/// Sin turno ni sede: cancelar no transporta plan.
/// </remarks>
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
