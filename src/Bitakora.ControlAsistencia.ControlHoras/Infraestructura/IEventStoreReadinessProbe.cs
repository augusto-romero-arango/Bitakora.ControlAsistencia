namespace Bitakora.ControlAsistencia.ControlHoras.Infraestructura;

public interface IEventStoreReadinessProbe
{
    Task VerificarAsync(CancellationToken ct);
}
