namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Issue #458: CC opaco (mismo trato que DispositivoId) -- se estampa tal cual, sin normalizacion ni
// interpretacion contra catalogo alguno. Asignar por primera vez y reemplazar son el mismo evento
// (PUT semantico, MEF-ADR-0043 paso 2): el ultimo valor asignado es el vigente.
public record CentroDeCostosAsignado(string CentroDeCostos);
