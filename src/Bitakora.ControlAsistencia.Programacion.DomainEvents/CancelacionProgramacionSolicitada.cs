namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Evento de event sourcing (privado). Se persiste en el stream de SolicitudCancelacionAggregateRoot.
/// No se publica al Service Bus.
/// </summary>
/// <remarks>
/// Issue #498: espejo de ProgramacionTurnoSolicitada para la operacion inversa -- cancelar dias
/// especificos ya programados de un colaborador. Colaborador tipa con el record propio de este
/// ensamblado (ColaboradorProgramado), igual que su gemela: sin sede ni turno, cancelar no
/// transporta plan.
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
