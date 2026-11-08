using Bitakora.ControlAsistencia.Programacion.DomainEvents;

namespace Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;

public sealed record JornadaDelCatalogo(Guid JornadaId, LimitesJornada Limites, ModalidadJornada? Modalidad = null, bool DescansoAcumulado = false)
{
    // "Sin modalidad" cuenta como un valor de la identidad (CA-ADR-0046 decision 12).
    internal bool TieneIdentidad(LimitesJornada limites, ModalidadJornada? modalidad) =>
        Limites.Equals(limites) && Equals(Modalidad, modalidad);
}

public interface ILectorLimitesJornada
{
    // Solo las Jornadas con esos limites, con su modalidad leida del stream: la vista LimitesDeJornada no la conoce.
    Task<IReadOnlyList<JornadaDelCatalogo>> ObtenerConLimitesAsync(
        Guid predeterminadaId, LimitesJornada limites, CancellationToken ct);
}
