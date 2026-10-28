namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public interface ILectorPlantillasTurnosPorJornada
{
    Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid jornadaId, CancellationToken ct);
}
