namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra el cierre de la vinculacion vigente de un colaborador,
/// con la fecha efectiva de terminacion. Se persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #349: payload plano, sin VOs ricos -- mismo criterio que VinculacionIniciada. SIN Motivo
/// (decision de refinamiento 2026-08-11): ninguna regla, consulta ni vista de este BC lo consume;
/// la fuente autoritativa del "por que" es RRHH/nomina, fuera de este dominio.
/// FechaEfectiva puede ser pasada (registro tardio) o futura (preaviso) -- el evento no valida
/// contra el reloj del servidor, esa regla vive en ColaboradorAggregateRoot.TerminarVinculacion.
/// No necesita ConfigurarSerializacion: tipo primitivo (DateOnly), STJ lo reconstruye sin ayuda --
/// mismo criterio que VinculacionIniciada/ProgramacionTurnoSolicitada.
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #349 "Consumidores: ninguno").
/// </remarks>
public sealed record VinculacionTerminada(DateOnly FechaEfectiva);
