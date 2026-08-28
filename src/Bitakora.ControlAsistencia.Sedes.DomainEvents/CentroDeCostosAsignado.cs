namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// El CC es opaco (mismo trato que DispositivoId): se estampa tal cual llega, sin normalizacion ni
// validacion contra catalogo alguno.
public record CentroDeCostosAsignado(string CentroDeCostos);
