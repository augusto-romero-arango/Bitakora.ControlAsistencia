namespace Bitakora.ControlAsistencia.Colaboradores.TerminarVinculacionFunction;

// Issue #349: comando para terminar la vinculacion vigente de un colaborador bajo control de
// asistencia (retiro/despido/promocion sin control -- el "por que" no le pertenece a este BC).
// Trigger: HTTP POST, Route: Colaboradores/Terminaciones.
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
    DateOnly FechaEfectiva);
