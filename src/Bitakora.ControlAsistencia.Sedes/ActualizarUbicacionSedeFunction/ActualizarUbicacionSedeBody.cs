namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Issue #457: body reducido a Ciudad/Direccion -- Codigo viaja en la ruta
// (sedes/{codigo}/ubicacion), no en el body (mismo criterio que CorregirNombresBody de
// Colaboradores, issue #377). Sin validator: ambos campos son opcionales, sin regla de forma que
// exigir (mismo criterio que RegistrarSedeValidator para estos dos campos).
public record ActualizarUbicacionSedeBody(string? Ciudad, string? Direccion);
