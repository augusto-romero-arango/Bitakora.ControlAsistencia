namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Terna de identidad del colaborador que persiste ProgramacionTurnoSolicitada. Payload propio de
/// esta isla (CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6): los tres ensamblados de eventos
/// no se referencian entre si, asi que el mapeo desde la forma del body HTTP vive en el Function
/// App (SolicitarProgramacionTurnoCommandHandler), el unico proyecto que ve ambas formas.
/// </summary>
/// <remarks>
/// Identificacion llega con el contrato del maestro Colaboradores ("{Tipo}-{Numero}", nunca
/// aplanada) y NombreCompleto ya concatenado: el cliente resuelve la terna contra el maestro y el
/// servidor NUNCA la compone (#330). Issue #436 (fase B del corte): reducido del quinteto a la
/// terna, lo que restablece la simetria con su gemelo de ControlHoras.DomainEvents -- reducido en
/// la fase A (#433) -- y con ResumenColaborador (PrivateEvents), la forma que cruza el bus.
///
/// Issue #340: el nombre anterior era Empleado, termino proscrito del lenguaje ubicuo por #330. El
/// reemplazo NO toma el nombre puro Colaborador -- ese pertenece al concepto rico del dominio
/// Colaboradores --: lleva calificador de intencion, mismo criterio anti-squatting que dio
/// SedeProgramada en vez de Sede (#331, #336).
///
/// No esta en IdentidadEventosProgramacion.TiposPersistidos: no tiene alias propio, y STJ no
/// persiste $type para payload anidado (MEF-ADR-0036 no aplica al TIPO). Reducirle campos exige
/// purgar los streams en el mismo despliegue: releidos con el tipo reducido, los eventos escritos
/// con el quinteto dejan Identificacion/NombreCompleto en null SIN excepcion (MEF-ADR-0036
/// seccion 5 por analogia).
///
/// Sin Equals custom: todos los campos son string, la igualdad por valor del record ya es correcta.
/// </remarks>
public record ColaboradorProgramado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
