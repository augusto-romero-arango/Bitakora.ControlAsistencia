namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Conserva el DispositivoId (no es un evento sin payload como CentroDeCostosRetirado) porque una
// sede tiene varios dispositivos instalados a la vez: el evento debe decir CUAL se retiro.
public record DispositivoRetirado(string DispositivoId);
