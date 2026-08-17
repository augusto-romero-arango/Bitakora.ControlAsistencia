namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Datos de identificacion del colaborador, propios del dominio Programacion.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de este ensamblado con
/// paridad exacta de campos con InformacionColaborador (PublicEvents) y DetalleColaborador
/// (PrivateEvents). No referencia ninguno de los dos -- Programacion.DomainEvents queda con cero
/// ProjectReference (CA-ADR-0029 enmendado por #317). Lo usa ProgramacionTurnoSolicitada, el
/// evento que SE PERSISTE.
///
/// Issue #340: el nombre anterior era Empleado, termino proscrito del lenguaje ubicuo por #330
/// (Colaborador es el concepto rico, mas amplio: da cabida a otras personas sujetas a control de
/// horario). El reemplazo NO toma el nombre puro Colaborador -- ese pertenece al concepto rico del
/// dominio Colaboradores --: lleva calificador de intencion, mismo criterio anti-squatting que dio
/// SedeProgramada en vez de Sede (#331, #336). Esto diverge deliberadamente de #319, que habia
/// elegido "nombre puro del lenguaje ubicuo" para los payloads. Es un gemelo deliberado del
/// ColaboradorProgramado de ControlHoras.DomainEvents: mismo nombre en otra isla, con paridad de
/// campos y sin referencia entre ambos (patron SedeProgramada, #336). Ese rename de #340 no toco
/// ninguna clave JSON.
///
/// Issue #401: el campo EmpleadoId paso a CodigoColaborador -- aqui SI cambia la clave JSON del
/// payload anidado. Sin mapeo: los streams de dev se purgan en el mismo despliegue (MEF-ADR-0036
/// seccion 5). Este record no esta en IdentidadEventosProgramacion.TiposPersistidos -- no tiene
/// alias propio, y STJ no persiste $type para payload anidado (MEF-ADR-0036 no aplica al TIPO;
/// precedente #319 CA-2, #322).
///
/// Sin comportamiento, sin Equals custom: todos los campos son string, la igualdad por valor
/// del record por defecto ya es correcta (mismo criterio que DetalleColaborador, issue #318).
/// </remarks>
public record ColaboradorProgramado(
    string CodigoColaborador,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
