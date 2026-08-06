namespace Bitakora.ControlAsistencia.Programacion.DomainEvents;

/// <summary>
/// Datos de identificacion del empleado, propios del dominio Programacion.
/// </summary>
/// <remarks>
/// Issue #319 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de este ensamblado con
/// paridad exacta de campos con InformacionEmpleado (PublicEvents) y DetalleEmpleado
/// (PrivateEvents). No referencia ninguno de los dos -- Programacion.DomainEvents queda con cero
/// ProjectReference (CA-ADR-0029 enmendado por #317). Nombre puro del lenguaje ubicuo (sin
/// calificador de rol): lo usa ProgramacionTurnoSolicitada, el evento que SE PERSISTE.
/// Sin comportamiento, sin Equals custom: todos los campos son string, la igualdad por valor
/// del record por defecto ya es correcta (mismo criterio que DetalleEmpleado, issue #318).
/// </remarks>
public record Empleado(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
