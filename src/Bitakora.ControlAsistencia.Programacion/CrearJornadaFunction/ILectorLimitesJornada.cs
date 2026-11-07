using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public sealed record JornadaDelCatalogo(Guid JornadaId, LimitesJornada Limites, ModalidadJornada? Modalidad = null);

public interface ILectorLimitesJornada
{
    Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerLimitesAsync(Guid predeterminadaId, CancellationToken ct);
}
