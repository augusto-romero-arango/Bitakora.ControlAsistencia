using System.Text.Json;
using Bitakora.ControlAsistencia.ReadModels.ControlHoras;
using Cosmos.MultiTenancy;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Bitakora.ControlAsistencia.ControlHoras.ListarTurnosVigentes;

// Issue #440: migracion de GET a QUERY (RFC 10008, MEF-ADR-0042 seccion 1) -- un rango de fechas
// obligatorio es un filtro estructurado. El nombre de la Function y su Route NO cambian al cruzar
// la frontera de verbo (MEF-ADR-0042 seccion 5 / MEF-ADR-0006 enmendado): solo el segundo
// argumento del HttpTriggerAttribute pasa de "get" a "query". Sigue siendo la via de consulta (a')
// de MEF-ADR-0035 (LINQ sobre session.Query<TView>()) contra la misma vista materializada
// TurnoVigente (#328): ninguna proyeccion nueva, ningun cambio al seam del worker.
//
// Migracion seca decidida con el humano al refinar (issue #440, "Contexto"): sin consumidores
// externos del GET, el verbo QUERY reemplaza al GET en el mismo PR -- no hay convivencia
// GET+QUERY sobre la misma plantilla.
//
// Guards del borde en el mismo orden que el precedente ListarAsistenciasDiarias (#427):
// Content-Type no-JSON -> 415 (verificado ANTES de leer el body: ante un Content-Type no-JSON,
// ReadFromJsonAsync lanza una excepcion que NO es JsonException y escaparia como 500 pese al catch
// de abajo); body ausente o JSON invalido -> 400; DesdeFecha/HastaFecha ausentes o rango invertido
// -> 422 (MEF-ADR-0042 seccion 3).
//
// Diferencia deliberada con el precedente (issue #440, "Diferencia con el precedente que el
// implementer debe respetar"): alli CodigoColaborador es obligatorio (pantalla de UN colaborador);
// aqui CodigoColaborador y SedeId son OPCIONALES -- su ausencia no produce 422 (CA-2): sin
// CodigoColaborador es el panorama del Programador (regresion #329), sin SedeId es la ausencia de
// filtro por sede (regresion #337).
//
// CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve ITenantResolver --
// nunca a un tenant id que llegue en el filtro (MEF-ADR-0028/skills/projections/read-apis.md,
// mitigacion estructural contra BOLA/IDOR). DesdeFecha/HastaFecha/CodigoColaborador/SedeId SI
// vienen del filtro: son el contrato de la consulta, no el tenant.
//
// Issue #337 (CA-2/CA-3/CA-4): SedeId filtra "dias donde AL MENOS un bloque rige en esa sede", por
// eso el predicado es Bloques.Any(b => b.SedeId == sedeId) y no un campo a nivel de TurnoVigente
// (la sede va por bloque, nunca por dia). Marten traduce esa igualdad dentro de una coleccion hija
// a containment JSONB -- data -> 'Bloques' @> '[{"SedeId": ...}]' --, la unica forma elegible para
// indice GIN (martendb.io/documents/querying/linq/child-collections.html). Esa misma semantica de
// containment resuelve CA-4/CA-5 sin rama explicita: un bloque sin la clave SedeId (franja sin
// sede, o documento proyectado antes de #336/#337) nunca contiene un sedeId no nulo.
public class FunctionEndpoint(IDocumentStore store, ITenantResolver tenantResolver)
{
    [Function("ListarTurnosVigentes")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "query", Route = "control-horas/turnos-vigentes")]
        HttpRequest req,
        CancellationToken ct)
    {
        // El 415 va ANTES de leer el body: ante un Content-Type no-JSON, ReadFromJsonAsync lanza
        // una excepcion que NO es JsonException y escaparia como 500 pese al catch de abajo.
        if (!req.HasJsonContentType())
            return new ObjectResult("La query exige Content-Type: application/json")
            { StatusCode = StatusCodes.Status415UnsupportedMediaType };

        FiltroListarTurnosVigentes? filtro;
        try
        {
            filtro = await req.ReadFromJsonAsync<FiltroListarTurnosVigentes>(ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult("El body de la query no es un JSON valido");
        }

        if (filtro is null)
            return new BadRequestObjectResult("El body de la query es obligatorio");

        // CA-2: DesdeFecha/HastaFecha son obligatorias pese a que CodigoColaborador y SedeId no lo
        // son -- su ausencia (ya sea individual o el filtro vacio completo) es 422, nunca 400.
        if (filtro.DesdeFecha is null || filtro.HastaFecha is null)
            return new ObjectResult("DesdeFecha y HastaFecha son obligatorios")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        // CA-3: rango invertido (HastaFecha < DesdeFecha) -> 422. Los filtros opcionales validos no
        // relajan esta validacion (CA-2 + CA-3, issue #337).
        if (filtro.DesdeFecha > filtro.HastaFecha)
            return new ObjectResult("DesdeFecha no puede ser posterior a HastaFecha")
            { StatusCode = StatusCodes.Status422UnprocessableEntity };

        var desde = filtro.DesdeFecha.Value;
        var hasta = filtro.HastaFecha.Value;
        var codigoColaborador = filtro.CodigoColaborador;
        var sedeId = filtro.SedeId;

        // CA-4: recorte de la cota de 31 dias, siempre hacia adelante desde `desde`.
        var rangoAplicado = RangoConsulta.Recortar(desde, hasta);

        // CA-1/CA-2: la QuerySession se abre SIEMPRE acotada al tenant que resuelve
        // ITenantResolver -- nunca a un tenant id que llegara por el filtro (mitigacion
        // estructural contra BOLA/IDOR, MEF-ADR-0028/skills/projections/read-apis.md).
        await using var session = store.QuerySession(tenantResolver.TenantId);

        // Composicion en pasos (no un unico Where con los filtros opcionales embebidos
        // condicionalmente): mas legible que anidar el ternario dentro de la expresion LINQ.
        IQueryable<TurnoVigente> query = session.Query<TurnoVigente>()
            .Where(v => v.Fecha >= desde && v.Fecha <= rangoAplicado.HastaAplicado);

        // CA-2: CodigoColaborador es opcional -- ausente = panorama de todos los colaboradores
        // (consulta del Programador); presente = filtrado a un solo colaborador (consulta del
        // Trabajador).
        if (!string.IsNullOrEmpty(codigoColaborador))
            query = query.Where(v => v.CodigoColaborador == codigoColaborador);

        // Issue #337, CA-2: "dias donde al menos un bloque rige en esa sede" -- un dia multi-sede
        // (turno partido Suba/Chapinero) aparece bajo cualquiera de las sedes de sus bloques.
        if (!string.IsNullOrEmpty(sedeId))
            query = query.Where(v => v.Bloques.Any(b => b.SedeId == sedeId));

        // Orden sugerido por la investigacion del planner (#329): por CodigoColaborador y luego
        // por Fecha -- estable para pintar grillas multi-colaborador x rango de fechas.
        var turnos = await query
            .OrderBy(v => v.CodigoColaborador)
            .ThenBy(v => v.Fecha)
            .ToListAsync(ct);

        var respuesta = new ListaTurnosVigentes(
            desde, rangoAplicado.HastaAplicado, rangoAplicado.RangoRecortado, turnos);

        // CA-4: nunca 404 -- un rango sin turnos vigentes es 200 con Turnos: [].
        return new OkObjectResult(respuesta);
    }
}
