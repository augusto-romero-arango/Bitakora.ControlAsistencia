namespace Bitakora.ControlAsistencia.Colaboradores.CorregirFechaInicioVinculacionFunction;

// Issue #352: comando para corregir la fecha de inicio de la ULTIMA vinculacion de un colaborador
// (tenga o no terminacion registrada, decision de refinamiento 2026-08-11) -- quinto comando del
// ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357). La fecha de inicio es un dato
// requerido que puede nacer errado (caso real: se registro ayer con la fecha mal) y necesita su
// enmienda.
// Trigger: HTTP POST, Route: Colaboradores/FechasInicio (el recurso que se reemplaza; la
// identificacion viaja en el body, decision vigente hasta #378 -- rutas orientadas a recurso --: el
// issue #381 cambio la representacion a "CC-79543210" justamente para que la llave sea apta como
// segmento de URI). Su gemelo CorregirNombres ya migro en el issue #377 a
// PUT colaboradores/{id}/nombres (MEF-ADR-0043 paso 2); este comando lo sigue en #378.
// Payload primitivo -- igual que los demas comandos del ciclo de vida (MEF-ADR-0039 decision 6,
// payload por rol): NUNCA reusa un tipo de Colaboradores.DomainEvents como campo. El handler
// construye TipoIdentificacion/Identificacion a partir de estos primitivos (parseo tipado unico en
// el borde, MEF-ADR-0037 seccion 2).
// FechaCorregida es REQUERIDA (DateOnly, sin default del servidor) -- doctrina bitemporal del BC:
// el tiempo de los hechos viene del cliente, nunca del reloj del servidor.
public record CorregirFechaInicioVinculacion(
    string TipoIdentificacion,
    string NumeroIdentificacion,
    DateOnly FechaCorregida);
