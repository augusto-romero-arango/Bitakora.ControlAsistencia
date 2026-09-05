namespace Bitakora.ControlAsistencia.Programacion.SmokeTests.Fixtures;

// Forma minima de SedeProgramada para asertar sobre el JSON persistido sin referenciar
// Programacion.DomainEvents desde los smoke tests (mismo criterio que DeadLetterMinimos).
// CentroDeCostos es opcional en el dominio y se persiste normalizado a null, asi que el default
// del record mantiene la igualdad por valor con las sedes que no lo declaran.
public sealed record SedeMinima(string Id, string Nombre, string? CentroDeCostos = null);
