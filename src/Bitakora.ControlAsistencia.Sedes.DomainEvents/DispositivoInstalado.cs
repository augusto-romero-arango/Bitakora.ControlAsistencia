namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// DispositivoId es opaco: ajeno al sistema, se estampa tal cual llega, sin normalizacion.
public record DispositivoInstalado(string DispositivoId);
