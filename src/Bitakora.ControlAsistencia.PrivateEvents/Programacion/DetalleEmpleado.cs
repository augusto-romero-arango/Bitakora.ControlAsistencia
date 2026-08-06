namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana del empleado que viaja en eventos privados intra-BC.
/// </summary>
/// <remarks>
/// Payload por rol (CA-ADR-0029 decision #5): duplica con paridad exacta de campos a
/// InformacionEmpleado (PublicEvents/Empleados) en vez de importarlo, porque los ensamblados de
/// eventos son tres islas sin referencias entre si (decision #2). El nombre simple es
/// deliberadamente distinto para que un using equivocado no compile en los proyectos que ven
/// ambos namespaces -- mismo criterio que RegistroDeMarcacionCreado frente a MarcacionRegistrada.
/// Sin Equals custom: todos los campos son string, asi que la igualdad por valor del record por
/// defecto ya es correcta (a diferencia de DetalleTurno/DetalleFranjaOrdinaria/DetalleSubFranja,
/// cuyas IReadOnlyList el record compararia por referencia -- MEF-ADR-0012).
/// </remarks>
public record DetalleEmpleado(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
