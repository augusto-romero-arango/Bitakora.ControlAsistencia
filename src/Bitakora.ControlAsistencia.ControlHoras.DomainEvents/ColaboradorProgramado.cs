namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Terna de identidad del colaborador que persiste TurnoDiarioAsignado. Payload propio de esta isla
/// (CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6): los tres ensamblados de eventos no se
/// referencian entre si, asi que el mapeo desde la forma del bus vive en el Function App, el unico
/// ensamblado que ve las tres islas (ProgramacionTurnoDiarioSolicitadaEventHandler).
///
/// Identificacion llega con el contrato del maestro Colaboradores ("{Tipo}-{Numero}", nunca
/// aplanada) y NombreCompleto ya concatenado: ni el aggregate ni el worker de proyecciones componen
/// nada a partir de este payload. NO tiene paridad de campos con InformacionColaborador
/// (PublicEvents), que sigue siendo el quinteto hasta la fase B del corte.
///
/// Comparte forma con ResumenColaborador de esta misma isla (terna de DepuracionDiaRecibida): son
/// dos eventos persistidos distintos, cada uno dueno de su payload, y se mantienen separados por
/// MEF-ADR-0018 (Rule of Three) -- unificarlos amarra la evolucion de un evento a la del otro.
///
/// No esta en IdentidadEventosControlHoras.TiposPersistidos: no tiene alias propio, y STJ no
/// persiste $type para payload anidado (MEF-ADR-0036 no aplica al TIPO). Reducirle campos exige
/// purgar los streams en el mismo despliegue: releidos con el tipo reducido, los eventos escritos
/// con el quinteto dejan Identificacion/NombreCompleto en null SIN excepcion (MEF-ADR-0036
/// seccion 5 por analogia).
///
/// Sin Equals custom: todos los campos son string, la igualdad por valor del record ya es correcta.
/// </summary>
public record ColaboradorProgramado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
