namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Issue #378 (MEF-ADR-0043 paso 1): body reducido a los 2 campos que no se derivan de la ruta --
// TipoIdentificacion/NumeroIdentificacion ya no llegan en el body, se derivan de {id}
// (colaboradores/{id}/vinculaciones). FunctionEndpoint compone el comando interno IniciarVinculacion
// (que SI conserva sus 4 campos primitivos, ver IniciarVinculacion.cs) a partir de este DTO +
// Identificacion.Parsear(id). Vive en el namespace del endpoint, no junto al comando: es forma de
// transporte del borde HTTP, no el comando interno (mismo criterio que CorregirNombresBody,
// issue #377).
// Issue #520: CodigoSede OPCIONAL -- si ya se conoce la sede del reingreso, evita una segunda
// peticion a AsignarSede. null = sin sede ("reingreso nace limpio" sigue siendo el default).
public record IniciarVinculacionBody(string CodigoColaborador, DateOnly FechaInicio, string? CodigoSede = null);
