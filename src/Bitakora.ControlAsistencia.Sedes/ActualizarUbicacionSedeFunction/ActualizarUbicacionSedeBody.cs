namespace Bitakora.ControlAsistencia.Sedes.ActualizarUbicacionSedeFunction;

// Sin Codigo: viaja en la ruta. Sin validator: ambos campos son opcionales y sin regla de forma.
public record ActualizarUbicacionSedeBody(string? Ciudad, string? Direccion);
