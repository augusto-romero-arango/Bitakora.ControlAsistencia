namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public interface IAseguradorJornadaPredeterminada
{
    Task<Guid> AsegurarAsync(CancellationToken ct);
}
