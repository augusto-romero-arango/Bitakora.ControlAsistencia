namespace Bitakora.ControlAsistencia.Sedes.Infraestructura;

public interface IEventStoreReadinessProbe
{
    Task VerificarAsync(CancellationToken ct);
}
