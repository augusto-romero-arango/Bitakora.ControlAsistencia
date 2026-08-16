namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #379 (MEF-ADR-0043 paso 4): body reducido a FechaEfectiva -- TipoIdentificacion/
// NumeroIdentificacion ya no viajan en el body, se derivan de {id} en la ruta
// (colaboradores/{id}/vinculaciones/{codigo}:terminar); Codigo se deriva de {codigo}.
// FunctionEndpoint compone el comando interno TerminarVinculacion (que SI conserva sus 4 campos,
// ver TerminarVinculacion.cs) a partir de este DTO + Identificacion.Parsear(id) + {codigo}. Vive
// en el namespace del endpoint, no junto al comando: es forma de transporte del borde HTTP, no el
// comando interno (mismo criterio que CorregirNombresBody/IniciarVinculacionBody).
public record TerminarVinculacionBody(DateOnly FechaEfectiva);
