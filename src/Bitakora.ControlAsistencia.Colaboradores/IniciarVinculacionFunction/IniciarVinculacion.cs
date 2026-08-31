namespace Bitakora.ControlAsistencia.Colaboradores.IniciarVinculacionFunction;

// Issue #378 (MEF-ADR-0043 paso 1): comando que inicia una vinculacion nueva sobre un colaborador
// EXISTENTE -- create disfrazado (paso 1 del test de precedencia): el criterio decidible no es el
// nombre del comando, es que eventos emite realmente, verificado contra la historia del stream:
// emite el MISMO evento que RegistrarColaborador (VinculacionIniciada). Absorbe y reemplaza a
// ReingresarColaborador (issue #350) -- mismos 4 campos primitivos, cero tipos nuevos de payload;
// "reingreso" sigue nombrando el escenario de negocio (CA-4), deja de nombrar la operacion.
// Trigger: HTTP POST, Route: colaboradores/{id}/vinculaciones -- {id} es
// Identificacion.ToString() ("CC-79543210"); TipoIdentificacion/NumeroIdentificacion se derivan
// alli via Identificacion.Parsear (MEF-ADR-0037 seccion 2). El body se reduce a CodigoColaborador +
// FechaInicio (IniciarVinculacionBody). Este record SIGUE siendo el comando interno con sus 4
// campos primitivos (mismo criterio que CorregirNombres post-#377): el endpoint lo compone desde
// ruta + body antes de despacharlo.
// Payload primitivo -- mismo criterio que TerminarVinculacion/RegistrarColaborador (MEF-ADR-0039
// decision 6, payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El
// handler construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado
// unico en el borde, MEF-ADR-0037 seccion 2).
// SIN nombres (corregir nombres es otra intencion, #351/#377 -- no se mezclan) y SIN motivo (la
// fuente autoritativa del "por que" es RRHH/nomina, sin lector en este BC) -- payload minimo
// (codigo + fecha, la identificacion viaja en la ruta), decision heredada de #350.
// FechaInicio es REQUERIDA (DateOnly, sin default del servidor) -- doctrina bitemporal del BC: el
// tiempo de los hechos viene del cliente, nunca del reloj del servidor (CA-1).
public record IniciarVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoColaborador,
    DateOnly FechaInicio,
    string? CodigoSede = null);
