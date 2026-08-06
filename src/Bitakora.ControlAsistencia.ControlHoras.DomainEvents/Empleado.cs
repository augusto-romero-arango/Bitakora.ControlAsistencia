namespace Bitakora.ControlAsistencia.ControlHoras.DomainEvents;

/// <summary>
/// Datos de identificacion del empleado, propios de este ensamblado (issue #322, payload por rol
/// -- CA-ADR-0029 decision #5 / MEF-ADR-0039 decision #6). Duplica con paridad exacta de campos a
/// InformacionEmpleado (PublicEvents.Empleados) y a DetalleEmpleado (PrivateEvents.Programacion)
/// en vez de importarlos: los tres ensamblados de eventos son tres islas sin referencias entre si
/// (CA-ADR-0029 decision #2). El mapeo entre estos tipos vive en el Function App, el unico
/// ensamblado que ve los tres (ProgramacionTurnoDiarioSolicitadaEventHandler.MapearEmpleado).
/// Sin Equals custom: todos los campos son string, asi que la igualdad por valor del record por
/// defecto ya es correcta (mismo criterio que DetalleEmpleado).
/// </summary>
public record Empleado(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
