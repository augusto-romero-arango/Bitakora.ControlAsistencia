namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana del colaborador que viaja en eventos privados intra-BC.
/// </summary>
/// <remarks>
/// Payload por rol (CA-ADR-0029 decision #5): duplica con paridad exacta de campos a
/// InformacionColaborador (PublicEvents/Colaboradores) en vez de importarlo, porque los ensamblados
/// de eventos son tres islas sin referencias entre si (decision #2). El nombre simple es
/// deliberadamente distinto para que un using equivocado no compile en los proyectos que ven
/// ambos namespaces -- mismo criterio que RegistroDeMarcacionCreado frente a MarcacionRegistrada.
///
/// Issue #340: el nombre anterior era DetalleEmpleado, termino proscrito del lenguaje ubicuo por
/// #330 (Colaborador es el concepto rico, mas amplio: da cabida a otras personas sujetas a control
/// de horario). El reemplazo conserva el calificador del dialecto Detalle* de este ensamblado
/// (DetalleSede, DetalleTurno, DetalleFranjaOrdinaria) y NO toma el nombre puro Colaborador -- ese
/// pertenece al concepto rico del dominio Colaboradores --: mismo criterio anti-squatting que dio
/// SedeProgramada en vez de Sede (#331, #336). Es el gemelo de bus interno de los
/// ColaboradorProgramado de Programacion/ControlHoras.DomainEvents y de InformacionColaborador
/// (PublicEvents), con paridad de campos. El rename no toca ninguna clave JSON del bus: solo
/// cambia el TIPO; los nombres de propiedad se conservan hasta #401.
///
/// Sin Equals custom: todos los campos son string, asi que la igualdad por valor del record por
/// defecto ya es correcta (a diferencia de DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja,
/// cuyas IReadOnlyList el record compararia por referencia -- MEF-ADR-0012).
/// </remarks>
public record DetalleColaborador(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
