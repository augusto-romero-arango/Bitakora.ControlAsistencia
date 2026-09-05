namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

// DTO del body, sin invariantes: PlantillaId lo genera el cliente.
public record CrearPlantillaSemanal(Guid PlantillaId, string Nombre, int Semanas);
