namespace Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;

// Puerto del lookup best-effort contra CuadroSemanalTurnos (CA-ADR-0034 decision 4, espejo de
// ILectorNombresTurno #497): el handler normaliza y decide, la consulta solo trae los nombres
// vigentes del catalogo de plantillas. No inyectar IDocumentStore/ITenantResolver directo en el
// CommandHandler: el harness de comando no fakea Marten y esta rama necesita cobertura unitaria
// con un fake manual.
public interface ILectorNombresPlantillaSemanal
{
    Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default);
}
