namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra el retiro de una etiqueta dinamica de la vinculacion
/// vigente de un colaborador. Se persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #355: payload plano -- CategoriaNormalizada (string), sin VO -- mismo criterio que
/// VinculacionTerminada. No necesita ConfigurarSerializacion: tipo primitivo, STJ lo reconstruye
/// sin ayuda.
/// Solo la forma NORMALIZADA (no la original): el retiro identifica la categoria por su clave en
/// el diccionario de la vinculacion (Etiqueta.CategoriaNormalizada, #353) -- el evento no necesita
/// la forma original porque no hay valor que preservar en el registro (a diferencia de
/// EtiquetaAsignada, que persiste la Etiqueta completa incluyendo su display).
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #355 "Consumidores: ninguno").
/// </remarks>
public sealed record EtiquetaRetirada(string CategoriaNormalizada);
