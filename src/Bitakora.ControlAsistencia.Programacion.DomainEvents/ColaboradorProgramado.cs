namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Terna de identidad del colaborador que persiste ProgramacionTurnoSolicitada. El mapeo desde el
/// body HTTP vive en SolicitarProgramacionTurnoCommandHandler, el unico proyecto que ve ambas formas.
/// </summary>
/// <remarks>
/// Identificacion llega con el formato del maestro ("{Tipo}-{Numero}", nunca aplanada) y
/// NombreCompleto ya concatenado: el cliente resuelve la terna contra el maestro y el servidor NUNCA
/// la compone. No se llama Colaborador: ese nombre pertenece al concepto rico del dominio Colaboradores.
///
/// No tiene alias propio y STJ no persiste $type para payload anidado: reducirle campos exige purgar
/// los streams en el mismo despliegue. Releidos con el tipo reducido, los eventos escritos con el
/// quinteto dejan Identificacion/NombreCompleto en null SIN excepcion (MEF-ADR-0036 seccion 5).
///
/// Sin Equals custom: todos los campos son string.
/// </remarks>
public record ColaboradorProgramado(
    string Identificacion,
    string CodigoColaborador,
    string NombreCompleto);
