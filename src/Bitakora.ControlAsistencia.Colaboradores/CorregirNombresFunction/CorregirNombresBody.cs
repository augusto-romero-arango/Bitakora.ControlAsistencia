namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #377 (MEF-ADR-0043 paso 2): body reducido a los 4 campos del nombre -- TipoIdentificacion
// y NumeroIdentificacion ya no viajan en el body, se derivan de {id} en la ruta
// (colaboradores/{id}/nombres). FunctionEndpoint compone el comando interno CorregirNombres (que SI
// conserva sus 6 campos, ver CorregirNombres.cs) a partir de este DTO + Identificacion.Parsear(id).
// Vive en el namespace del endpoint, no junto al comando: es forma de transporte del borde HTTP, no
// el comando interno (mismo criterio que AsignarEtiquetaBody, issue #376).
public record CorregirNombresBody(
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido);
