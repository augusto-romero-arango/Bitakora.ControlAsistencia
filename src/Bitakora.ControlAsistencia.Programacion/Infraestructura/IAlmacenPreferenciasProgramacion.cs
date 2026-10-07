using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

public interface IAlmacenPreferenciasProgramacion
{
    Task<PreferenciasProgramacion?> LeerAsync(string tenantId, CancellationToken ct);

    Task IniciarAsync(string tenantId, JornadaCreada jornada, JornadaPredeterminadaAsignada asignacion,
        CancellationToken ct);
}
