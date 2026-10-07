namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Que plantillas usan una Jornada: lo consumen la sincronizacion de limites y la desactivacion de
// la Jornada. Sin paginacion ni tope: composicion interna, no superficie de consumidor final (CA-ADR-0038).
public interface ILectorPlantillasPorJornada
{
    Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid jornadaId, CancellationToken ct);
}
