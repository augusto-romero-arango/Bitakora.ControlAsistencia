using Bitakora.ControlAsistencia.Programacion.CrearJornadaFunction;
using Bitakora.ControlAsistencia.Programacion.DomainEvents;
using Bitakora.ControlAsistencia.Programacion.Entities;
using Bitakora.ControlAsistencia.Programacion.CrearPlantillaSemanalFunction;
using Bitakora.ControlAsistencia.Programacion.CrearTurnoFunction;
using Bitakora.ControlAsistencia.ReadModels.Programacion;
using Cosmos.MultiTenancy;
using Marten;

namespace Bitakora.ControlAsistencia.Programacion.Infraestructura;

// Adaptador unico de los lookups de solo lectura sobre el read-side propio de Programacion. Espejo
// de LectorReadSideSedes (#477): la QuerySession se abre siempre acotada al tenant que resuelve
// ITenantContext (MEF-ADR-0028/CA-ADR-0027).
//
// Los dos puertos declaran ObtenerNombresAsync con la misma firma, asi que ambos se implementan de
// forma explicita: la clase no expone superficie publica propia y ninguna de las dos vistas queda
// arbitrariamente privilegiada como "la" del tipo concreto. Solo se resuelve por interfaz (DI).
public class LectorReadSideProgramacion(IDocumentStore store, ITenantContext tenantContext)
    : ILectorNombresTurno, ILectorNombresPlantillaSemanal, ILectorLimitesJornada
{
    async Task<IReadOnlyList<string>> ILectorNombresTurno.ObtenerNombresAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantContext.TenantId);
        return await session.Query<FichaTurno>().Select(f => f.Nombre).ToListAsync(ct);
    }

    async Task<IReadOnlyList<string>> ILectorNombresPlantillaSemanal.ObtenerNombresAsync(CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantContext.TenantId);
        return await session.Query<CuadroSemanalTurnos>().Select(c => c.Nombre).ToListAsync(ct);
    }

    async Task<IReadOnlyList<JornadaDelCatalogo>> ILectorLimitesJornada.ObtenerLimitesAsync(
        Guid predeterminadaId, CancellationToken ct)
    {
        await using var session = store.QuerySession(tenantContext.TenantId);
        var vista = await session.Query<LimitesDeJornada>().ToListAsync(ct);

        // Proyeccion Async atrasada: la predeterminada recien materializada se lee de su stream.
        var idTexto = predeterminadaId.ToString();
        if (vista.All(l => l.Id != idTexto))
        {
            var jornada = await session.Events.AggregateStreamAsync<Jornada>(idTexto, token: ct);
            if (jornada is not null)
                vista = [.. vista, jornada.ComoVista()];
        }

        return vista.Select(l => new JornadaDelCatalogo(Guid.Parse(l.Id), LimitesJornada.Crear(
            HorasYMinutos.Crear(l.HorasSemanalesEnMinutos / MinutosPorHora, l.HorasSemanalesEnMinutos % MinutosPorHora),
            HorasYMinutos.Crear(l.TopeDiarioEnMinutos / MinutosPorHora, l.TopeDiarioEnMinutos % MinutosPorHora),
            HorasYMinutos.Crear(l.MinimoDiarioEnMinutos / MinutosPorHora, l.MinimoDiarioEnMinutos % MinutosPorHora),
            l.DiasDescansoPorSemana))).ToList();
    }

    private const int MinutosPorHora = 60;
}
