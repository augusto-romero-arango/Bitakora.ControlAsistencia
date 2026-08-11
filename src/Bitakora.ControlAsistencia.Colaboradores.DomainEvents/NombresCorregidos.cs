namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la correccion de los nombres de un colaborador. Se
/// persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #351: el nombre cuenta un hecho de dominio -- la correccion de un nombre mal digitado, o
/// la del dueno legitimo del documento tras reusar el stream (sesion 2026-08-07/10) -- en vez de un
/// generico "DatosActualizados" sin semantica, descartado en el refinamiento.
/// Payload rico: Nombre (NombreColaborador, VO de #348) viaja completo, mismo criterio que
/// ColaboradorRegistrado. No declara ConfigurarSerializacion propio: la del VO, que
/// ConfiguracionSerializacionColaboradores ya registra, basta para reconstruir el ctor publico del
/// record.
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #351 "Consumidores: ninguno").
/// </remarks>
public sealed record NombresCorregidos(NombreColaborador Nombre);
