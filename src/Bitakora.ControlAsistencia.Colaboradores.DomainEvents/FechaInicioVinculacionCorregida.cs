namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la correccion de la fecha de inicio de la ULTIMA
/// vinculacion de un colaborador (tenga o no terminacion registrada). Se persiste en el stream de
/// ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #352: payload plano (DateOnly) -- mismo criterio que VinculacionTerminada. Nombre propio
/// ("FechaInicioVinculacionCorregida"), no una reutilizacion de VinculacionIniciada: a diferencia
/// del reingreso (#350, mismo hecho que el registro), esta es una ENMIENDA de un dato que ya
/// existia, no el nacimiento de una vinculacion nueva -- amerita su propio tipo (CA-ADR-0029: un
/// evento no conoce su comando, pero tampoco disfraza un hecho distinto bajo un tipo existente).
/// No necesita ConfigurarSerializacion: tipo primitivo (DateOnly), STJ lo reconstruye sin ayuda --
/// mismo criterio que VinculacionTerminada/VinculacionIniciada.
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #352 "Consumidores: ninguno").
/// </remarks>
public sealed record FechaInicioVinculacionCorregida(DateOnly FechaInicio);
