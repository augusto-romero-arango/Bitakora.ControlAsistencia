namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// Issue #354: comando para anular la terminacion registrada de la ULTIMA vinculacion de un
// colaborador -- sexto comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357)
// y el mas simple de la cadena: una sola regla, cero fechas en el payload. Resuelve dos casos
// reales con el mismo hecho: el arrepentimiento del preaviso (Maria anuncio su salida al 30 y el
// 27 decide quedarse) y la fecha de terminacion errada (se compone con TerminarVinculacion, ver
// ColaboradorAggregateRoot.AnularTerminacion).
// Issue #379 (MEF-ADR-0043 paso 4): Trigger HTTP POST, Route:
// colaboradores/{id}/vinculaciones/{codigo}:anular-terminacion. TipoIdentificacion/
// NumeroIdentificacion se derivan de {id} via Identificacion.Parsear (MEF-ADR-0037 seccion 2);
// Codigo se deriva de {codigo} (URL-safe garantizado por #387). SIN body en absoluto: los tres
// campos de este comando viajan completos en la ruta -- reemplaza el POST
// Colaboradores/Terminaciones/Anulaciones (identificacion en el body, decision vigente hasta
// #378/#379).
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado unico en
// el borde, MEF-ADR-0037 seccion 2).
// Codigo viaja intacto (sin validacion de formato adicional en el borde: ya paso por el complex
// segment {codigo}:anular-terminacion) -- la comparacion contra el codigo vigente vive en el
// aggregate (Tell-don't-Ask, MEF-ADR-0012, CA-5): CodigoNoCorresponde es la razon de rechazo
// evaluada PRIMERA.
// SIN fecha ni motivo: anular no lleva payload propio -- el hecho es el evento mismo
// (TerminacionAnulada, sin campos).
public record AnularTerminacion(string TipoIdentificacion, string NumeroIdentificacion, string Codigo);
