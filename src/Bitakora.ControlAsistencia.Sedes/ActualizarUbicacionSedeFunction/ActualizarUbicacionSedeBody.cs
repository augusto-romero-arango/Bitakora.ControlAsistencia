namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Sin Codigo: viaja en la ruta (sedes/{codigo}/ubicacion), no en el body. Sin validator propio:
// ambos campos son opcionales y no tienen regla de forma.
public record ActualizarUbicacionSedeBody(string? Ciudad, string? Direccion);
