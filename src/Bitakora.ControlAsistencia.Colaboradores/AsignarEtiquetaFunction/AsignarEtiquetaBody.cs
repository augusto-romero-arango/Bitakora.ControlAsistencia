namespace Bitakora.ControlAsistencia.Colaboradores.AsignarEtiquetaFunction;

// Issue #376 (MEF-ADR-0043 paso 2): body reducido a { "valor": "..." } -- TipoIdentificacion,
// NumeroIdentificacion y Categoria ya no viajan en el body, se derivan de la ruta
// (colaboradores/{id}/etiquetas/{categoria}). FunctionEndpoint compone el comando AsignarEtiqueta
// (que SI conserva sus 4 campos, ver AsignarEtiqueta.cs) a partir de este DTO + los segmentos de
// ruta ya parseados. Vive en el namespace del endpoint, no junto al comando: es forma de
// transporte del borde HTTP, no el comando interno (mismo criterio que FichaColaboradorRespuesta
// en ObtenerFichaColaborador/FunctionEndpoint.cs -- un DTO de borde, excepcion Rule of Three).
public record AsignarEtiquetaBody(string Valor);
