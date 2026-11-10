namespace Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;

// Lookup best-effort de los nombres vigentes del catalogo; el handler normaliza y decide. Es puerto, y
// no IDocumentStore directo en el handler, porque el harness de comando no fakea Marten.
public interface ILectorNombresTurno
{
    Task<IReadOnlyList<string>> ObtenerNombresAsync(CancellationToken ct = default);
}
