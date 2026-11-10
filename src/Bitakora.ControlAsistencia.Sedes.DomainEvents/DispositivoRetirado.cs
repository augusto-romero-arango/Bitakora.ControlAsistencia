namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// A diferencia de CentroDeCostosRetirado lleva payload: una sede tiene varios dispositivos a la vez.
public record DispositivoRetirado(string DispositivoId);
