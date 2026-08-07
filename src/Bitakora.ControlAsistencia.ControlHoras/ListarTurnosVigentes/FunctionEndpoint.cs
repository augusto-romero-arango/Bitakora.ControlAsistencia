using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

// Issue #329: tercera Function GET del BC sobre la vista materializada TurnoVigente (#328) --
// ninguna proyeccion nueva, ningun cambio al seam del worker (issue #329, "Necesidad de lectura":
// "Este issue NO crea read model, ni clase de proyeccion, ni lifecycle: solo la Function GET de
// listado"). Feature folder propio (un namespace por query, skills/projections/naming.md y
// read-apis.md): esta clase FunctionEndpoint no colisiona con las demas homonimas del ensamblado
// (ObtenerTurnoDiario/ListarTurnosDiarios/ObtenerTurnoVigente/RegistrarMarcacionFunction/...)
// porque cada una vive en su propio namespace.
//
// Ruta sin {empleadoId}/{fecha}: reutiliza el mismo segmento de recurso
// "control-horas/turnos-vigentes" que ObtenerTurnoVigente (#328) -- naming.md: "una query reutiliza
// el mismo segmento de recurso que ya usa el comando/query de ese recurso". Sin colision de
// plantilla: la de ObtenerTurnoVigente lleva dos segmentos adicionales ({empleadoId}/{fecha}).
//
// CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
// nunca a un tenant id que llegue por query string (MEF-ADR-0028/skills/projections/read-apis.md,
// mitigacion estructural contra BOLA/IDOR). desde/hasta/empleadoId SI vienen del query string: son
// el filtro del recurso, no el tenant.
//
// FASE ROJA (projection-test-writer): Run es un stub. projection-implementer completa el parseo de
// desde/hasta/empleadoId, la consulta session.Query<TurnoVigente>() (via (a'), read-apis.md), el
// recorte de rango (CA-3, mismo patron de tope que RangoConsulta de #290) y el mapeo al envelope de
// respuesta (CA-1). El constructor SI debe resolver limpio del contenedor -- eso es lo que verifica
// el test de composicion de ComposicionServiciosTests, hermano de MEF-ADR-0029: no depende de que
// Run este implementado.
//
// Issue #329 "Capas de test esperadas": config-test del worker y unit tests de proyeccion NO
// aplican a este issue (sin proyeccion nueva) -- unica capa read-side declarada es el test de
// composicion de esta Function, que verifica solo la resolucion de IDocumentStore/ITenantResolver
// por constructor (carve-out de coverage de Functions GET, MEF-ADR-0035/issue #371): el
// comportamiento real de Run (parseo, recorte, filtro por empleadoId, mapeo al envelope) queda para
// projection-implementer y para el smoke test contra dev (CA-8 del precedente #290).
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarTurnosVigentes")]
    public Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "control-horas/turnos-vigentes")]
        HttpRequest req,
        CancellationToken ct)
    {
        // Referencia minima a store/tenantResolver para evitar CS9113 (parametro de constructor
        // primario sin lectura) mientras Run no tiene implementacion real -- projection-implementer
        // reemplaza este cuerpo por la consulta/recorte/mapeo real (CA-1 a CA-4).
        _ = store;
        _ = tenantResolver;
        throw new NotImplementedException();
    }
}
