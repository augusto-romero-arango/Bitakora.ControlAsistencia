namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;

// Lookup best-effort de los nombres vigentes (CA-ADR-0034 decision 4); el handler normaliza y decide.
// Es puerto porque el harness de comando no fakea Marten.
public interface ILectorNombresPlantillaTurnos
{
    Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default);
}
