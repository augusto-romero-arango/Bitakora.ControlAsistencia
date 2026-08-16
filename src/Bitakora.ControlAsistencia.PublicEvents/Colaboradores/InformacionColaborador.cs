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
/// Sede (#331, #336). Es el gemelo publico, con paridad de campos, de los ColaboradorProgramado de
/// Programacion/ControlHoras.DomainEvents y de DetalleColaborador (PrivateEvents). El rename no
/// toca ninguna clave JSON del bus: solo cambia el TIPO y su namespace; los nombres de propiedad
/// se conservan hasta #401.
/// </remarks>
public record InformacionColaborador(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
