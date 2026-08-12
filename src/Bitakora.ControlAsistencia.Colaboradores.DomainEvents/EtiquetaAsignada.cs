namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la asignacion (o sobrescritura) de una etiqueta dinamica
/// a la vinculacion vigente de un colaborador. Se persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #355: payload rico -- Etiqueta (VO de #353) viaja completo, con su doble forma (original +
/// normalizada) de categoria y valor, mismo criterio que NombresCorregidos con NombreColaborador.
/// No declara ConfigurarSerializacion propio: la de Etiqueta, que ConfiguracionSerializacionColaboradores
/// debe registrar (fase verde -- ver ConfiguracionSerializacionColaboradores.ConfigurarResolver),
/// basta para reconstruir el ctor publico del record.
/// Un valor por categoria (CA-2): este evento SIEMPRE representa el estado final de esa categoria --
/// el aggregate lo emite tanto al crear una categoria nueva como al sobrescribir el valor de una
/// existente (ColaboradorAggregateRoot.AsignarEtiqueta decide cuando, este evento nunca lo distingue).
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #355 "Consumidores: ninguno").
/// </remarks>
public sealed record EtiquetaAsignada(Etiqueta Etiqueta);
