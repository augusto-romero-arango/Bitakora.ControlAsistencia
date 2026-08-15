namespace Bitakora.ControlAsistencia.Colaboradores.CorregirNombresFunction;

// Issue #351: comando para corregir los nombres de un colaborador existente. Cuarto comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357) y el mas simple: sin reglas de
// estado -- solo exige existencia del colaborador, nunca vigencia de su vinculacion (los nombres
// son de la PERSONA, no de la vinculacion, decision de refinamiento 2026-08-11).
// Issue #377 (MEF-ADR-0043 paso 2): Trigger HTTP PUT, Route: colaboradores/{id}/nombres (reemplazo
// completo del VO atomico NombreColaborador, direccionable por {id}). TipoIdentificacion/
// NumeroIdentificacion ya no llegan en el body -- el endpoint los deriva de {id} via
// Identificacion.Parsear (MEF-ADR-0037 seccion 2); el body se reduce a los 4 campos del nombre
// (CorregirNombresBody). Este record SIGUE siendo el comando interno tal cual (forma sin cambios,
// mismo criterio que AsignarEtiqueta post-#376): el endpoint lo compone desde ruta + body antes de
// despacharlo.
// Payload primitivo -- igual que RegistrarColaborador/TerminarVinculacion (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion/NombreColaborador a partir de estos primitivos
// (parseo tipado unico en el borde, MEF-ADR-0037 seccion 2).
public record CorregirNombres(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string PrimerNombre,
    string? SegundoNombre,
    string PrimerApellido,
    string? SegundoApellido);
