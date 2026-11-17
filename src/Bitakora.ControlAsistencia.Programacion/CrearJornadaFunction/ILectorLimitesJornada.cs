using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public sealed record JornadaDelCatalogo(Guid JornadaId, LimitesJornada Limites);

public interface ILectorLimitesJornada
{
    // Solo las Jornadas con esos limites: dos Jornadas no pueden compartirlos.
    Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerConLimitesAsync(
        Guid predeterminadaId, LimitesJornada limites, CancellationToken ct);
}
