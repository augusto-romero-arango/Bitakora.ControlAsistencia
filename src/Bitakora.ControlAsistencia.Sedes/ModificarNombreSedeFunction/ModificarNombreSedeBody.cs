namespace Bitakora.ControlAsistencia.Sedes.ModificarNombreSedeFunction;

// Issue #457: body reducido a Nombre -- Codigo viaja en la ruta (sedes/{codigo}/nombre), no en el
// body (mismo criterio que CorregirNombresBody de Colaboradores, issue #377).
public record ModificarNombreSedeBody(string Nombre);
