namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

public interface ILectorPlantillasPorTurno
{
    Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid turnoId, CancellationToken ct);
}
