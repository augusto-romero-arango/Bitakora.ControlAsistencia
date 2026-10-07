namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Puerto de lectura interno de Programacion: que plantillas usan una Jornada. Lo consumen la
// reaccion que sincroniza limites (#884) y la desactivacion de Jornada (#869). Sin paginacion ni
// tope: es composicion interna, no superficie para un consumidor final (CA-ADR-0038).
public interface ILectorPlantillasPorJornada
{
    Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid jornadaId, CancellationToken ct);
}
