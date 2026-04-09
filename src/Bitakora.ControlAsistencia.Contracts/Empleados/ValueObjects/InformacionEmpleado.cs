namespace Bitakora.ControlAsistencia.Contracts.Empleados.ValueObjects;

/// <summary>
/// Datos de identificacion del empleado que viajan en eventos entre dominios.
/// </summary>
public record InformacionEmpleado(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
