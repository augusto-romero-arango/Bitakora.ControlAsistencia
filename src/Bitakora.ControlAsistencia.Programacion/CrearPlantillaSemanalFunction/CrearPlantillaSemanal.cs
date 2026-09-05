namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

// Issue #620: record = DTO sin invariantes, constructor primario publico (mismo patron que
// CrearTurno, MEF-ADR-0015). PlantillaId lo genera el cliente.
public record CrearPlantillaSemanal(Guid PlantillaId, string Nombre, int Semanas);
