namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra el nacimiento de un colaborador bajo control de
/// asistencia. Se persiste en el stream de ColaboradorAggregateRoot, junto con VinculacionIniciada,
/// en un solo commit.
/// </summary>
/// <remarks>
/// Issue #330: primer evento persistido del dominio Colaboradores. Payload rico -- Identificacion y
/// NombreColaborador (VOs de #348) llegan ya validados desde el handler; este evento no protege
/// ninguna invariante propia (a diferencia de MarcacionRegistrada/TurnoCreado, que si validan), asi
/// que es un record simple sin ctor privado ni factory Crear (MEF-ADR-0012: record para DTOs sin
/// invariantes).
/// No declara ConfigurarSerializacion propio: Identificacion y NombreColaborador ya traen la suya
/// (ConfiguracionSerializacionColaboradores.ConfigurarResolver las delega), y el ctor publico del
/// record es reconstruible por STJ sin ayuda adicional una vez que esos dos VOs esten registrados --
/// mismo patron que ProgramacionTurnoSolicitada (ConfiguracionSerializacionProgramacion.cs, "su ctor
/// publico es el unico, asi que STJ lo resuelve sin ayuda").
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores.
/// </remarks>
public sealed record ColaboradorRegistrado(Identificacion Identificacion, NombreColaborador Nombre);
