namespace Bitakora.ControlAsistencia.Colaboradores.DomainEvents;

/// <summary>
/// Evento de event sourcing que registra la correccion de los nombres de un colaborador. Se
/// persiste en el stream de ColaboradorAggregateRoot.
/// </summary>
/// <remarks>
/// Issue #351: cuarto comando del ciclo de vida de ColaboradorAggregateRoot (desglose #348-#357),
/// y el mas simple -- sin reglas de estado. El nombre "NombresCorregidos" cuenta un hecho de
/// dominio (sesion 2026-08-07/10: cedula equivocada corregida al reingresar el dueno legitimo del
/// documento), en vez de un generico "DatosActualizados" sin semantica (descartado en el
/// refinamiento 2026-08-11).
/// Payload rico: Nombre (NombreColaborador, VO de #348) viaja completo -- mismo criterio que
/// ColaboradorRegistrado. No declara ConfigurarSerializacion propio: NombreColaborador ya trae la
/// suya (ConfiguracionSerializacionColaboradores.ConfigurarResolver la delega) y el ctor publico
/// del record es reconstruible por STJ sin ayuda adicional una vez que ese VO este registrado --
/// mismo patron que ColaboradorRegistrado.
/// Solo exige EXISTENCIA del colaborador, no vigencia de su vinculacion (decision de refinamiento
/// 2026-08-11): los nombres son de la PERSONA, no de la vinculacion -- corregirlos sobre un
/// colaborador con vinculacion terminada es valido.
/// No cruza el bus (sin marker IPrivateEvent/IPublicEvent): event-sourcing puro, sin consumidores
/// (issue #351 "Consumidores: ninguno").
/// </remarks>
public sealed record NombresCorregidos(NombreColaborador Nombre);
