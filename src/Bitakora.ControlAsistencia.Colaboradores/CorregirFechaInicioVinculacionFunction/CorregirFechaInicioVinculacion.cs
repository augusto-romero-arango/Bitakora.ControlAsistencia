namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Issue #352: comando para corregir la fecha de inicio de la ULTIMA vinculacion de un colaborador
// (tenga o no terminacion registrada, decision de refinamiento 2026-08-11) -- quinto comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357). La fecha de inicio es un dato
// requerido que puede nacer errado (caso real: se registro ayer con la fecha mal) y necesita su
// enmienda.
// Issue #379 (MEF-ADR-0043 paso 4): Trigger HTTP POST, Route:
// colaboradores/{id}/vinculaciones/{codigo}:corregir-fecha-inicio. TipoIdentificacion/
// NumeroIdentificacion ya no llegan en el body -- el endpoint los deriva de {id} via
// Identificacion.Parsear (MEF-ADR-0037 seccion 2); Codigo se deriva de {codigo} (URL-safe
// garantizado por #387); el body se reduce a FechaCorregida (CorregirFechaInicioVinculacionBody).
// Este record SIGUE siendo el comando interno tal cual (forma sin cambios salvo el campo Codigo
// nuevo, mismo criterio que CorregirNombres/IniciarVinculacion/TerminarVinculacion post-#377/#378/
// #379): el endpoint lo compone desde ruta + body. Su gemelo CorregirNombres ya migro en el issue
// #377 a PUT colaboradores/{id}/nombres (MEF-ADR-0043 paso 2); este comando lo sigue en #378/#379.
// Codigo viaja intacto (sin validacion de formato adicional en el borde: ya paso por el complex
// segment {codigo}:corregir-fecha-inicio) -- la comparacion contra el codigo vigente vive en el
// aggregate (Tell-don't-Ask, MEF-ADR-0012, CA-5): CodigoNoCorresponde es la razon de rechazo
// evaluada PRIMERA, antes incluso de la idempotencia (SinCambios).
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado unico en
// el borde, MEF-ADR-0037 seccion 2).
// FechaCorregida es REQUERIDA (DateOnly, sin default del servidor) -- doctrina bitemporal del BC:
// el tiempo de los hechos viene del cliente, nunca del reloj del servidor.
public record CorregirFechaInicioVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Codigo,
    DateOnly FechaCorregida);
