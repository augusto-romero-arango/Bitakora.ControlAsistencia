namespace Bitakora.ControlAsistencia.PublicEvents.Colaboradores;

/// <summary>
/// Datos de identificacion del colaborador que viajan en eventos entre dominios.
/// </summary>
/// <remarks>
/// Issue #340: el nombre anterior era InformacionEmpleado (en la carpeta/namespace Empleados),
/// termino proscrito del lenguaje ubicuo por #330 (Colaborador es el concepto rico, mas amplio: da
/// cabida a otras personas sujetas a control de horario). Conserva su calificador de intencion
/// vigente (Informacion*) y NO toma el nombre puro Colaborador -- ese pertenece al concepto rico
/// del dominio Colaboradores --: mismo criterio anti-squatting que dio SedeProgramada en vez de
/// Sede (#331, #336). Es el gemelo publico, con paridad de campos, del ColaboradorProgramado de
/// Programacion.DomainEvents. Ese rename de #340 solo cambio el TIPO y su namespace, sin tocar
/// ninguna clave JSON del bus. Issue #433 (fase A): el lado de ControlHoras y el payload del bus
/// interno ya se redujeron a la terna de identidad y dejaron de espejar este quinteto; la fase B
/// reduce tambien este tipo y el body HTTP que lo alimenta.
///
/// Issue #401: el campo EmpleadoId paso a CodigoColaborador -- aqui SI cambia la clave JSON que
/// viaja por el bus. Es el identificador que emite el maestro Colaboradores (#330): el vocabulario
/// del Published Language queda unificado con el del dominio que lo origina.
/// </remarks>
public record InformacionColaborador(
    string CodigoColaborador,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
