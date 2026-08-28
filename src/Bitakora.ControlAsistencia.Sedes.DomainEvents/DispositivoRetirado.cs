namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #460: gemelo de retiro de DispositivoInstalado. Conserva el DispositivoId (no solo un
// evento sin payload como CentroDeCostosRetirado) porque una sede puede tener varios dispositivos
// instalados a la vez -- el evento debe decir CUAL se retiro.
public record DispositivoRetirado(string DispositivoId);
