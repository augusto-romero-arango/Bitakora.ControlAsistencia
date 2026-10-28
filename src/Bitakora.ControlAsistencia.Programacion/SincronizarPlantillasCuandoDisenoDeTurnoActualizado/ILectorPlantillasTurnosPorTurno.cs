namespace Bitakora.ControlAsistencia.Programacion.SincronizarPlantillasCuandoDisenoDeTurnoActualizado;

public interface ILectorPlantillasTurnosPorTurno
{
    Task<IReadOnlyList<string>> ObtenerPlantillaIdsAsync(Guid turnoId, CancellationToken ct);
}
