namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// Puerto del lookup best-effort contra FichaTurno (CA-ADR-0030, precedente ILectorUbicacionDispositivo
// #477): el handler normaliza y decide, la consulta solo trae los nombres vigentes del catalogo.
// No inyectar IDocumentStore/ITenantResolver directo en el CommandHandler: el harness de comando no
// fakea Marten y esta rama necesita cobertura unitaria con un fake manual.
public interface ILectorNombresTurno
{
    Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default);
}
