namespace Bitakora.ControlAsistencia.PrivateEvents.Programacion;

/// <summary>
/// Representacion plana del empleado que viaja en eventos privados intra-BC.
/// </summary>
/// <remarks>
/// Issue #318 (tres islas, MEF-ADR-0039 decision 2 y 6): payload propio de PrivateEvents con
/// paridad exacta de campos con InformacionEmpleado (PublicEvents/Empleados). No referencia ese
/// tipo -- PrivateEvents queda sin ProjectReference a PublicEvents (CA-ADR-0029 enmendado por
/// #317). El nombre difiere a proposito de "InformacionEmpleado" para que un using equivocado
/// no resuelva en silencio en los ~20 archivos que importan ambos namespaces (MEF-ADR-0039
/// decision 6).
/// Sin comportamiento, sin Equals custom: todos los campos son string, la igualdad por valor
/// del record por defecto es correcta (a diferencia de DetalleTurno/DetalleFranjaOrdinaria/
/// DetalleSubFranja, que tienen IReadOnlyList y necesitan Equals propio, ADR-0015).
/// </remarks>
public record DetalleEmpleado(
    string EmpleadoId,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string Nombres,
    string Apellidos);
