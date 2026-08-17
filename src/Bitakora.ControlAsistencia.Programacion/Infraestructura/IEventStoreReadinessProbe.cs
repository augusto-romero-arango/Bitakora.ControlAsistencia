namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public interface IEventStoreReadinessProbe
{
    Task VerificarAsync(CancellationToken ct);
}
