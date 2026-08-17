namespace Bitakora.ControlAsistencia.Colaboradores.Infraestructura;

public interface IEventStoreReadinessProbe
{
    Task VerificarAsync(CancellationToken ct);
}
