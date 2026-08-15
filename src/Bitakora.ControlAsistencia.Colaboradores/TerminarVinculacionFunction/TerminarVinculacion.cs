namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #349: comando para terminar la vinculacion vigente de un colaborador bajo control de
// asistencia (retiro/despido/promocion sin control -- el "por que" no le pertenece a este BC).
// Issue #379 (MEF-ADR-0043 paso 4): Trigger HTTP POST, Route:
// colaboradores/{id}/vinculaciones/{codigo}:terminar. TipoIdentificacion/NumeroIdentificacion ya
// no llegan en el body -- el endpoint los deriva de {id} via Identificacion.Parsear (MEF-ADR-0037
// seccion 2); Codigo se deriva de {codigo} (URL-safe garantizado por #387); el body se reduce a
// FechaEfectiva (TerminarVinculacionBody). Este record SIGUE siendo el comando interno tal cual
// (forma sin cambios salvo el campo Codigo nuevo, mismo criterio que CorregirNombres/
// IniciarVinculacion post-#377/#378): el endpoint lo compone desde ruta + body.
// Codigo viaja intacto (sin validacion de formato adicional en el borde: ya paso por el complex
// segment {codigo}:terminar) -- la comparacion contra el codigo vigente vive en el aggregate
// (Tell-don't-Ask, MEF-ADR-0012, CA-5): CodigoNoCorresponde es la razon de rechazo evaluada
// PRIMERA.
// Payload primitivo -- igual que RegistrarColaborador (MEF-ADR-0039 decision 6, payload por rol):
// NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler construye
// TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado unico en el
// borde, MEF-ADR-0037 seccion 2).
// FechaEfectiva es REQUERIDA (DateOnly, sin default del servidor) -- puede ser pasada (registro
// tardio) o futura (preaviso); nunca se valida contra el reloj del servidor en ninguna direccion
// (decision de refinamiento 2026-08-11, doctrina bitemporal del BC).
// SIN Motivo (eliminado en el refinamiento): la fuente autoritativa del "por que" es RRHH/nomina,
// sin lector en este BC.
public record TerminarVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Codigo,
    DateOnly FechaEfectiva);
