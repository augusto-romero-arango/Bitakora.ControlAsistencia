namespace Bitakora.ControlAsistencia.Colaboradores.ReingresarColaboradorFunction;

// Issue #350: comando para reingresar a un colaborador bajo control de asistencia (regreso a rol
// operativo, recontratacion con el mismo documento) -- tercer comando del ciclo de vida de
// ColaboradorAggregateRoot (desglose #348-#357). Opera contra el stream EXISTENTE del colaborador:
// la identificacion es la misma persona, lo que nace es una vinculacion nueva con su propio codigo
// transaccional.
// Trigger: HTTP POST, Route: Colaboradores/Reingresos.
// Payload primitivo -- mismo criterio que TerminarVinculacion/RegistrarColaborador (MEF-ADR-0039
// decision 6, payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El
// handler construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado
// unico en el borde, MEF-ADR-0037 seccion 2).
// SIN nombres (corregir nombres es otra intencion, #351 -- no se mezclan) y SIN motivo (la fuente
// autoritativa del "por que" es RRHH/nomina, sin lector en este BC) -- payload minimo
// (identificacion + codigo + fecha), decision de refinamiento 2026-08-11.
// FechaInicio es REQUERIDA (DateOnly, sin default del servidor) -- doctrina bitemporal del BC: el
// tiempo de los hechos viene del cliente, nunca del reloj del servidor (CA-4).
public record ReingresarColaborador(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string CodigoColaborador,
    DateOnly FechaInicio);
