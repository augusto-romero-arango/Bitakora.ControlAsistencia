namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la asignacion (o reasignacion) de la sede de la
/// vinculacion vigente de un colaborador. Se persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #465: payload plano -- SOLO el codigo de la sede (referencia pura al maestro de Sedes),
/// nunca el nombre ni el centro de costos (esos se estampan en los hechos operativos, DiaAprobado
/// #489, nunca aqui): un rename o cambio de CC de la sede no toca streams de colaborador. Islas
/// (CA-ADR-0029): PROHIBIDO referenciar Sedes.DomainEvents.
/// No necesita ConfigurarSerializacion: tipo primitivo (string), STJ lo reconstruye sin ayuda --
/// mismo criterio que VinculacionTerminada/VinculacionIniciada.
/// Asignar y reasignar emiten el MISMO evento (un evento no conoce su comando, CA-ADR-0029): este
/// tipo siempre representa el reemplazo completo de la sede, sin distincion de "primera vez" vs
/// "cambio" -- sin evento de retiro (decision de refinamiento: no existe caso de negocio de volver
/// a "sin sede").
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #465 "Consumidores: ninguno" -- la vista del hermano #519 lo consume desde el event
/// store, no desde un bus).
/// </remarks>
public sealed record SedeAsignada(string CodigoSede);
