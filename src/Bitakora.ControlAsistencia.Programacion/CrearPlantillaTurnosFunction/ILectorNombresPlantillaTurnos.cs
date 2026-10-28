namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaTurnosFunction;

// Puerto del lookup best-effort contra CuadroDeTurnos (CA-ADR-0034 decision 4): el handler
// normaliza y decide, la consulta solo trae los nombres vigentes. El harness de comando no fakea
// Marten, por eso el handler no inyecta IDocumentStore directo.
public interface ILectorNombresPlantillaTurnos
{
    Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default);
}
