namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// El CC es opaco: se estampa tal cual llega, sin normalizar ni validar contra un catalogo.
public record CentroDeCostosAsignado(string CentroDeCostos);
