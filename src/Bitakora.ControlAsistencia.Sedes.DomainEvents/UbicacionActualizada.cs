namespace Bitakora.ControlAsistencia.Sedes.DomainEvents;

// Ciudad+Direccion viajan juntas como valor atomico: el evento reemplaza ambas, nunca hace merge
// parcial de los nulos que trae.
public record UbicacionActualizada(string? Ciudad, string? Direccion);
