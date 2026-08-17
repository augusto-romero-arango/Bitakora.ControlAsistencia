namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Datos de identificacion del colaborador, propios de este ensamblado (issue #322, payload por rol
/// -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica con paridad exacta de campos a
/// InformacionColaborador (PublicEvents.Colaboradores) y a DetalleColaborador
/// (PrivateEvents.Programacion) en vez de importarlos: los tres ensamblados de eventos son tres
/// islas sin referencias entre si (CA-ADR-0029 decision #2). El mapeo entre estos tipos vive en el
/// Function App, el unico ensamblado que ve los tres
/// (ProgramacionTurnoDiarioSolicitadaEventHandler.MapearColaboradorProgramado).
///
/// Issue #340: el nombre anterior era Empleado, termino proscrito del lenguaje ubicuo por #330
/// (Colaborador es el concepto rico, mas amplio: da cabida a otras personas sujetas a control de
/// horario). El reemplazo NO toma el nombre puro Colaborador -- ese pertenece al concepto rico del
/// dominio Colaboradores --: lleva calificador de intencion, mismo criterio anti-squatting que dio
/// SedeProgramada en vez de Sede (#331, #336). Es un gemelo deliberado del ColaboradorProgramado de
/// Programacion.DomainEvents: mismo nombre simple en otra isla, con paridad de campos y sin
/// referencia entre ambos (patron SedeProgramada, #336). Ese rename de #340 no toco ninguna clave
/// JSON.
///
/// Issue #401: el campo EmpleadoId paso a CodigoColaborador -- aqui SI cambia la clave JSON del
/// payload anidado. Sin mapeo: los streams de dev se purgan en el mismo despliegue (MEF-ADR-0036
/// seccion 5). Este record no esta en IdentidadEventosControlHoras.TiposPersistidos -- no tiene
/// alias propio, y STJ no persiste $type para payload anidado (MEF-ADR-0036 no aplica al TIPO;
/// precedente #319 CA-2, #322).
///
/// Sin Equals custom: todos los campos son string, asi que la igualdad por valor del record por
/// defecto ya es correcta (mismo criterio que DetalleColaborador).
/// </summary>
public record ColaboradorProgramado(
    string CodigoColaborador,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
