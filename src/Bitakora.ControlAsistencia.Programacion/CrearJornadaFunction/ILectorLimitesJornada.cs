using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public sealed record JornadaDelCatalogo(Guid JornadaId, LimitesJornada Limites, ModalidadJornada? Modalidad = null);

public interface ILectorLimitesJornada
{
    Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerLimitesAsync(Guid predeterminadaId, CancellationToken ct);

    // Solo las Jornadas con esos limites, con su modalidad leida del stream: la vista LimitesDeJornada no la conoce.
    Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerConLimitesAsync(
        Guid predeterminadaId, LimitesJornada limites, CancellationToken ct);
}
