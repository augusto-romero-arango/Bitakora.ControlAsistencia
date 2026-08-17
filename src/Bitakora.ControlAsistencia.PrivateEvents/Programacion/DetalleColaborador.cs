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
/// (PublicEvents), con paridad de campos. Ese rename de #340 solo cambio el TIPO, sin tocar
/// ninguna clave JSON del bus.
///
/// Issue #401: el campo EmpleadoId paso a CodigoColaborador -- aqui SI cambia la clave JSON que
/// viaja por el bus interno. Sin mapeo (nada de JsonPropertyName): productor y consumidor de este
/// contrato viven en el mismo Bounded Context y se despliegan juntos.
///
/// Sin Equals custom: todos los campos son string, asi que la igualdad por valor del record por
/// defecto ya es correcta (a diferencia de DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja,
/// cuyas IReadOnlyList el record compararia por referencia -- MEF-ADR-0012).
/// </remarks>
public record DetalleColaborador(
    string CodigoColaborador,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
