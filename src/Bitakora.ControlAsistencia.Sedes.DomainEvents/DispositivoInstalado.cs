namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #460: DispositivoId es opaco (mismo trato que CentroDeCostosAsignado.CentroDeCostos) --
// ajeno al sistema, se estampa tal cual llega, sin normalizacion.
public record DispositivoInstalado(string DispositivoId);
