namespace Bitakora.ControlAsistencia.Colaboradores.AnularTerminacionFunction;

// Issue #354: comando para anular la terminacion registrada de la ULTIMA vinculacion de un
// colaborador -- sexto comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357)
// y el mas simple de la cadena: una sola regla, cero fechas en el payload. Resuelve dos casos
// reales con el mismo hecho: el arrepentimiento del preaviso (Maria anuncio su salida al 30 y el
// 27 decide quedarse) y la fecha de terminacion errada (se compone con TerminarVinculacion, ver
// ColaboradorAggregateRoot.AnularTerminacion).
// Trigger: HTTP POST, Route: Colaboradores/Terminaciones/Anulaciones (la anulacion como
// sub-recurso de las terminaciones, #349; la identificacion viaja en el body porque su
// representacion "CC:79543210" contiene ":", hostil como segmento de URL).
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado unico en
// el borde, MEF-ADR-0037 seccion 2).
// SIN fecha ni motivo: anular no lleva payload propio -- el hecho es el evento mismo
// (TerminacionAnulada, sin campos).
public record AnularTerminacion(string TipoIdentificacion, string NumeroIdentificacion);
