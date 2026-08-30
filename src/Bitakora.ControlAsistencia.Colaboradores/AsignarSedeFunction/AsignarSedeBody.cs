namespace Bitakora.ControlAsistencia.Colaboradores.AsignarSedeFunction;

// Issue #465: body reducido a { "codigoSede": "..." } -- TipoIdentificacion/NumeroIdentificacion no
// viajan en el body, se derivan de la ruta (colaboradores/{id}/sede). FunctionEndpoint compone el
// comando AsignarSede a partir de este DTO + {id} ya parseado. Vive en el namespace del endpoint,
// no junto al comando: es forma de transporte del borde HTTP (mismo criterio que AsignarEtiquetaBody).
public record AsignarSedeBody(string CodigoSede);
